using Microsoft.Extensions.Hosting;
using Mqtt.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mqtt.ItemGenerator
{
    public class ItemStatsService : BackgroundService
    {
        private readonly ItemGeneratorConfig _config;
        private MqttManager _mqtt = default!;
        private readonly Timer _reportStatusTimer;
        private int _currentCount;
        
        public ItemStatsService(ItemGeneratorConfig config)
        {
            _config = config;
            _reportStatusTimer = PrepareReportTimer();
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            Console.WriteLine($"[{DateTime.UtcNow}]\tBackground ItemStatsProcessor task is running...");
            if (_config.EnableTermination)
            {
                _mqtt = await MqttManagerFactory.CreateDefault(stoppingToken) ?? throw new ArgumentNullException(nameof(_mqtt));

                await StartGatheringTerminatedItemStats();
            }
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItems termination is disabled no stats are being gathered.");
            }
        }
        private async Task StartGatheringTerminatedItemStats()
        {
            await _mqtt.SubscribeTopicAsync(TopicsDefinition.ItemsTerminated(), async (payload, sourceTs, receiveTs, hopms) =>
            {
                var item = payload.Array.DeserializeItem();
                if (item != null)
                {
                    _currentCount++;

                    await PublishItemDetails(item, sourceTs, receiveTs, hopms).ConfigureAwait(false);
                    await RemoveItemFromStatusAsync(item);

                }
            });
        }

        private async Task RemoveItemFromStatusAsync(Item item)
        {
            await Task.Delay(_config.TerminationRetentionMilliseconds - 500);
            //Remove Item from status
            await _mqtt.RemoveStatusAsync(TopicsDefinition.ItemStatus(item.Id));
        }

        private async Task PublishItemDetails(Item item, DateTime sourceTs, DateTime receiveTs, double hopms)
        {
            if (item == null || item.Timestamps == null)
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItem is null or has no timestamps.");
                return;
            }

            if (item.Timestamps.Count > 1)
            {
                //Total Diference beteween creation timestamp and termination timestamp
                double lifecycleDurationMilliseconds = (item.Timestamps.LastOrDefault().Value - item.Timestamps.FirstOrDefault().Value).TotalMilliseconds;

                //Publish item to IoTMQ
                var itemWithRawTs = new
                {
                    item.Id,
                    item.BatchId,
                    RawTimestamps = JsonSerializer.Serialize(item.Timestamps),
                    item.ItemStatus,
                    LifecycleTotalMilliseconds = lifecycleDurationMilliseconds,
                    SourceTimestamp = sourceTs,
                    TargetTimestamp = receiveTs,
                    LastHopLatencyMilliseconds = hopms
                };
                await _mqtt.PublishMessageAsync(itemWithRawTs.ToUtf8Bytes(), TopicsDefinition.ItemsProcessed()).ConfigureAwait(false);
            }
        }

        private Timer PrepareReportTimer()
        {
            return new Timer((state) =>
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\t{_currentCount} items stats processed.");
            }, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }
    }
}
