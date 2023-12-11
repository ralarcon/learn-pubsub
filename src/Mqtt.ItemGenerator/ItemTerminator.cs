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
        private readonly MqttManager _iotmqBridge;
        private readonly Timer _reportStatusTimer;
        private int _currentCount;

        public ItemTerminator(ItemGeneratorConfig config, MqttManager mqtt, MqttManager iotmqBridge)
        {
            _config = config;
            _mqtt = mqtt;
            _iotmqBridge = iotmqBridge;
            _reportStatusTimer = PrepareReportTimer();
        }


        public async Task StartTerminatingItemsAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tReady to remove items from zone '{_config.TerminationZone}' after 30 seconds of arrival.");
            
            await _mqtt.SubscribeTopicAsync(TopicsDefinition.Items(_config.TerminationZone), async (payload) =>
            {
                if (payload.Array != null)
                {
                    var item = payload.Array.DeserializeItem();   
                    if (item != null)
                    {
                        item.Timestamps?.Add($"{_config.TerminationZone}_terminated".ToLower(), DateTime.UtcNow);
                        item.ItemStatus = ItemStatusEnum.Delivered;
                        await _mqtt.PublishMessageAsync(item.ToItemBytes(), TopicsDefinition.ItemsTerminated());
                        _currentCount++;
                    }
                }
            });

            await TerminateItemsAsync();
        }

        private async Task TerminateItemsAsync()
        {
            await _mqtt.SubscribeTopicAsync(TopicsDefinition.ItemsTerminated(), async (payload) =>
            {
                if (payload.Array != null)
                {
                    //Delay a bit to mark the status to avoid colisions
                    await Task.Delay(500);

                    var item = payload.Array.DeserializeItem();
                    if (item != null)
                    {
                        await CalulateAndPublishLatenciesAsync(item);

                        await RemoveItemFromStatusAsync(item);
                    }
                }
            });
        }

        private async Task RemoveItemFromStatusAsync(Item item)
        {
            //Update item position to destination
            ItemPosition itemPosition = new()
            {
                Id = item.Id,
                BatchId = item.BatchId,
                Zone = _config.TerminationZone,
                Position = "Destination",
                Status = item.ItemStatus,
                TimeStamp = DateTime.UtcNow
            };
            await _mqtt.PublishStatusAsync(itemPosition.ToItemPositionBytes(), TopicsDefinition.ItemStatus(item.Id));

            await Task.Delay(_config.TerminationRetentionMilliseconds - 500);

            //Remove Item from status
            await _mqtt.PublishStatusAsync(new byte[] { }, TopicsDefinition.ItemStatus(item.Id));
        }

        private async Task CalulateAndPublishLatenciesAsync(Item item)
        {
            //Calculate ItemLatencies and publish to a topic
            ItemLatencies itemLatencies = new()
            {
                Id = item.Id,
                BatchId = item.BatchId,
                Timestamps = item.Timestamps,
                Latencies = new Dictionary<string, TimeSpan>()
            };
            if (item.Timestamps?.Count > 1)
            {
                for (int i = 1; i < item.Timestamps.Count; i++)
                {
                    itemLatencies.Latencies.Add(item.Timestamps.ElementAt(i).Key, item.Timestamps.ElementAt(i).Value - item.Timestamps.ElementAt(i - 1).Value);
                }
            }

            if (_config.EnableBridgeToIoTMQ)
            {
                await _iotmqBridge.PublishMessageAsync(itemLatencies.ToUtf8Bytes(), _config.IoTMqTopic);
            }
            else
            {
                await _mqtt.PublishMessageAsync(itemLatencies.ToUtf8Bytes(), TopicsDefinition.ItemsLatencies());
            }
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
