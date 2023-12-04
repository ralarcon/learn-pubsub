using Mqtt.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ItemGenerator
{
    internal class ItemTerminator
    {
        private readonly ItemGeneratorConfig _config;
        private readonly MqttManager _mqtt;
        private readonly Timer _reportStatusTimer;
        private int _currentCount;

        public ItemTerminator(ItemGeneratorConfig config, MqttManager mqtt)
        {
            _config = config;
            _mqtt = mqtt;
            _reportStatusTimer = PrepareReportTimer();
        }


        public async Task StartTerminatingItemsAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tReady to remove items from zone '{_config.TerminationZone}' after 30 seconds of arrival.");
            
            await _mqtt.SubscribeTopicAsync(TopicsDefinition.Items(_config.TerminationZone), async (payload) =>
            {
                if (payload.Array != null)
                {
                    var item = await payload.Array.DeserializeAsync<Item>();   
                    if (item != null)
                    {
                        item.Timestamps?.Add($"{_config.TerminationZone}_terminated".ToLower(), DateTime.UtcNow);
                        item.ItemStatus = ItemStatusEnum.Delivered;
                        await _mqtt.PublishMessageAsync(await item.ToJsonByteArrayAsync(), TopicsDefinition.ItemsTerminated(_config.TerminationZone));
                        _currentCount++;
                    }
                }
            });

            await RemoveItemStatusAsync();
        }

        private async Task RemoveItemStatusAsync()
        {
            await _mqtt.SubscribeTopicAsync(TopicsDefinition.ItemsTerminated(_config.TerminationZone), async (payload) =>
            {
                if (payload.Array != null)
                {
                    //Delay a bit to mark the status to avoid colisions
                    await Task.Delay(500);

                    var item = await payload.Array.DeserializeAsync<Item>();
                    if (item != null)
                    {
                        ItemPosition itemPosition = new()
                        {
                            Id = item.Id,
                            BatchId = item.BatchId,
                            Zone = _config.TerminationZone,
                            Position = "Destination",
                            Status = item.ItemStatus,
                            TimeStamp = DateTime.UtcNow
                        };
                        await _mqtt.PublishStatusAsync(await itemPosition.ToJsonByteArrayAsync(), TopicsDefinition.ItemStatus(item.Id));

                        await Task.Delay(_config.TerminationRetentionMilliseconds - 500);

                        await _mqtt.PublishStatusAsync(new byte[] { }, TopicsDefinition.ItemStatus(item.Id));
                    }
                }
            });
        }

        private Timer PrepareReportTimer()
        {
            return new Timer((state) =>
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\t{_currentCount} items terminated.");
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }
    }
}
