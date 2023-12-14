using Mqtt.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.TimeZoneInfo;

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
            Console.WriteLine($"[{DateTime.UtcNow}]\tReady to remove items from zone '{_config.ItemsTermination}' after 30 seconds of arrival.");
            
            await _mqtt.SubscribeTopicAsync(TopicsDefinition.Items(_config.ItemsTermination), async (payload) =>
            {
                if (payload.Array != null)
                {
                    var item = payload.Array.DeserializeItem();   
                    if (item != null)
                    {
                        item.Timestamps?.Add($"{_config.ItemsTermination}_terminated".ToLower(), DateTime.UtcNow);
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
                Zone = _config.ItemsTermination,
                Position = "Destination",
                Status = item.ItemStatus,
                TimeStamp = DateTime.UtcNow
            };
            await _mqtt.PublishStatusAsync(itemPosition.ToItemPositionBytes(), TopicsDefinition.ItemStatus(item.Id));

            await Task.Delay(_config.TerminationRetentionMilliseconds - 500);

            //Remove Item from status
            await _mqtt.RemoveStatusAsync(TopicsDefinition.ItemStatus(item.Id));
        }

        private async Task CalulateAndPublishLatenciesAsync(Item item)
        {
            List<ItemTransitionLatency> itemLatencies = new();

            if (item.Timestamps?.Count > 1)
            {
                for (int i = 1; i < item.Timestamps.Count; i++)
                {
                    //itemLatencies.Latencies.Add(item.Timestamps.ElementAt(i).Key, item.Timestamps.ElementAt(i).Value - item.Timestamps.ElementAt(i - 1).Value);

                    var source = string.Empty;
                    var target = string.Empty;
                    var transitionType = ItemTransitionTypeEnum.Unknown;

                    if (item.Timestamps.ElementAt(i).Key.Contains("_to_"))
                    {
                        source = item.Timestamps.ElementAt(i).Key.Split("_to_")[0];
                        target = item.Timestamps.ElementAt(i).Key.Split("_to_")[1];

                        //Zone Transition
                        if (!IsConveyor(source) && IsConveyor(target))
                        {
                            transitionType = ItemTransitionTypeEnum.ZoneEnter;
                        }
                        else if (IsConveyor(source) && IsConveyor(target) && target.StartsWith(GetZone(source)))
                        {
                            transitionType = ItemTransitionTypeEnum.ConveyorChain;
                        }
                        else if (IsConveyor(source) && !IsConveyor(target))
                        {
                            transitionType = ItemTransitionTypeEnum.ZoneExit;
                        }
                    }
                    else
                    {
                        if (item.Timestamps.ElementAt(i).Key.EndsWith("_in"))
                        {
                            target = item.Timestamps.ElementAt(i).Key.Split("_in")[0];
                            source = GetZone(target);
                            transitionType = ItemTransitionTypeEnum.ConveyorEnter;
                        }

                        if (item.Timestamps.ElementAt(i).Key.EndsWith("_out"))
                        {
                            target = item.Timestamps.ElementAt(i).Key.Split("_out")[0];
                            source = GetZone(target);
                            transitionType = ItemTransitionTypeEnum.ConveyorTransport;
                        }

                        if(item.Timestamps.ElementAt(i).Key.EndsWith("_terminated"))
                        {
                            target = item.Timestamps.ElementAt(i).Key.Split("_terminated")[0];
                            source = GetZone(target);
                            transitionType = ItemTransitionTypeEnum.Termination;
                        }
                    }

                    itemLatencies.Add(new ItemTransitionLatency()
                    {
                        Id = item.Id,
                        BatchId = item.BatchId,
                        TransitionType = transitionType,
                        SourceZone = GetZone(source),
                        TargetZone = GetZone(target),
                        TimestampSourceName = item.Timestamps.ElementAt(i - 1).Key,
                        TimestampTargetName = item.Timestamps.ElementAt(i).Key,
                        TimestampSource = item.Timestamps.ElementAt(i - 1).Value,
                        TimestampTarget = item.Timestamps.ElementAt(i).Value,
                        LatencyMilliseconds = (item.Timestamps.ElementAt(i).Value - item.Timestamps.ElementAt(i - 1).Value).TotalMilliseconds
                    });
                }
            }


            foreach (var itemLatency in itemLatencies)
            {
                if (_config.EnableBridgeToIoTMQ)
                {
                    await _iotmqBridge.PublishMessageAsync(itemLatency.ToUtf8Bytes(), TopicsDefinition.ItemsLatencies());
                }
                else
                {
                    await _mqtt.PublishMessageAsync(itemLatency.ToUtf8Bytes(), TopicsDefinition.ItemsLatencies());
                }
            }

            if(_config.EnableBridgeToIoTMQ)
            {
                //Publish item to IoTMQ
                var itemWithRawTs = new
                {
                    item.Id,
                    item.BatchId,
                    RawTimestamps = String.Join("; ", item.Timestamps.Select(x => $"{x.Key} = {x.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}").ToArray()),
                    item.ItemStatus
                };
                await _iotmqBridge.PublishMessageAsync(itemWithRawTs.ToUtf8Bytes(), TopicsDefinition.ItemsProcessed());
            }
        }

        private bool IsConveyor(string source)
        {
            return Regex.IsMatch(source, @"[a-zA-Z]{3,}_c\d+");
        }

        private string GetZone(string source)
        {
            if (IsConveyor(source))
            {
                return source.Split("_")[0];
            }
            else
            {
                return source;
            };
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
