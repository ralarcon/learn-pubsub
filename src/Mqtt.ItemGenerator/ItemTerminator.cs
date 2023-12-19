using Mqtt.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
                var timestamp = DateTime.UtcNow;
                if (payload.Array != null)
                {
                    var item = payload.Array.DeserializeItem();   
                    if (item != null)
                    {
                        item.Timestamps?.Add($"{_config.ItemsTermination}_terminated".ToLower(), timestamp);
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
                    var sourceTimestamp = item.Timestamps.ElementAt(i - 1).Key;
                    var targetTimestamp = item.Timestamps.ElementAt(i).Key;

                    ItemTransitionTypeEnum transitionType = GetTransitionType(sourceTimestamp, targetTimestamp);

                    itemLatencies.Add(new ItemTransitionLatency()
                    {
                        Id = item.Id,
                        BatchId = item.BatchId,
                        TransitionType = transitionType,
                        SourceZone = GetZone(sourceTimestamp),
                        TargetZone = GetZone(targetTimestamp),
                        TimestampSourceName = sourceTimestamp,
                        TimestampTargetName = targetTimestamp,
                        TimestampSource = item.Timestamps.ElementAt(i - 1).Value,
                        TimestampTarget = item.Timestamps.ElementAt(i).Value,
                        LatencyMilliseconds = (item.Timestamps.ElementAt(i).Value - item.Timestamps.ElementAt(i - 1).Value).TotalMilliseconds
                    });
                }
                await PublishLatenciesToBridge(itemLatencies);

                await PublishItemDetailsToBridge(item, itemLatencies);
            }
        }

        private ItemTransitionTypeEnum GetTransitionType(string source, string target)
        {
            var transitionType = ItemTransitionTypeEnum.Unknown;
            if (source.EndsWith("_created"))
            {
                transitionType = ItemTransitionTypeEnum.ZoneEnter;
            }
            else if (target.EndsWith("_terminated"))
            {
                transitionType = ItemTransitionTypeEnum.Termination;
            }
            else if (TimestampIsTransition(target))
            {
                if(!TransitionIsConveyorChain(source) && TransitionSourceIsConveyor(source) && !TransitionIsConveyorChain(target) && TransitionTargetIsConveyor(target))
                {
                    transitionType = ItemTransitionTypeEnum.ZoneEnter;
                }
                else if (!TransitionIsConveyorChain(target) && (!TransitionSourceIsConveyor(source) && TransitionTargetIsConveyor(target)))
                {
                    transitionType = ItemTransitionTypeEnum.ZoneEnter;
                }
                else if (TimestampIsConveyorOut(source) && TransitionIsConveyorChain(target))
                {
                    transitionType = ItemTransitionTypeEnum.ConveyorChain;
                }
                else if (TimestampIsConveyorOut(source) && !TransitionTargetIsConveyor(target))
                {
                    transitionType = ItemTransitionTypeEnum.ZoneExit;
                }
            }
            else if (TimestampIsTransition(source) && TimestampIsConveyorIn(target) && !TransitionIsConveyorChain(target))
            {
                transitionType = ItemTransitionTypeEnum.ConveyorEnter;
            }
            else if (TimestampIsConveyorIn(source) && TimestampIsConveyorOut(target))
            {
                transitionType = ItemTransitionTypeEnum.ConveyorTransport;
            }
            else
            {
                transitionType = ItemTransitionTypeEnum.Unknown;
            }
            return transitionType;
        }

        private async Task PublishItemDetailsToBridge(Item item, List<ItemTransitionLatency> itemLatencies)
        {
            if(item == null || item.Timestamps == null)
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItem is null or has no timestamps.");
                return;
            }

            if (_config.EnableBridgeToIoTMQ && item.Timestamps.Count > 1)
            {
                //Total Diference beteween creation timestamp and termination timestamp
                double lifecycleDurationMilliseconds = (item.Timestamps.LastOrDefault().Value - item.Timestamps.FirstOrDefault().Value).TotalMilliseconds;

                //Total Latency without ConveyorTransport 
                double totalLatency = lifecycleDurationMilliseconds - itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ConveyorTransport).Sum(x => x.LatencyMilliseconds);

                //Publish item to IoTMQ
                var itemWithRawTs = new
                {
                    item.Id,
                    item.BatchId,
                    RawTimestamps = String.Join("; ", item.Timestamps.Select(x => $"{x.Key} = {x.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}").ToArray()),
                    item.ItemStatus,

                    LifecycleTotalMilliseconds = lifecycleDurationMilliseconds,
                    LatencyTotal = totalLatency,
                    TransitionCount = itemLatencies.Count,
                    TransitionAvg = totalLatency / itemLatencies.Count,

                    ConveyorCount = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ConveyorEnter).DefaultIfEmpty().Select(x => x?.SourceZone).Count(),
                    ConveyorEnterTotal = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ConveyorEnter).DefaultIfEmpty().Sum(x => x?.LatencyMilliseconds),
                    ConveyorEnterAvg = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ConveyorEnter).DefaultIfEmpty().Average(x => x?.LatencyMilliseconds),

                    ConveyorTransportTotal = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ConveyorTransport).DefaultIfEmpty().Sum(x => x?.LatencyMilliseconds),
                    ConveyorTransportAvg = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ConveyorTransport).DefaultIfEmpty().Average(x => x?.LatencyMilliseconds),

                    ConveyorChainTotal = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ConveyorChain).DefaultIfEmpty().Sum(x => x?.LatencyMilliseconds),
                    ConveyorChainAvg = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ConveyorChain).DefaultIfEmpty().Average(x => x?.LatencyMilliseconds),

                    ZoneCount = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ZoneEnter).DefaultIfEmpty().Select(x => x?.SourceZone).Count(),
                    ZoneEnterTotal = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ZoneEnter).DefaultIfEmpty().Sum(x => x?.LatencyMilliseconds),
                    ZoneEnterAvg = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ZoneEnter).DefaultIfEmpty().Average(x => x?.LatencyMilliseconds),

                    ZoneExitTotal = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ZoneExit).DefaultIfEmpty().Sum(x => x?.LatencyMilliseconds),
                    ZoneExitAvg = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.ZoneExit).DefaultIfEmpty().Average(x => x?.LatencyMilliseconds),

                    Termination = itemLatencies.Where(x => x.TransitionType == ItemTransitionTypeEnum.Termination).DefaultIfEmpty().Sum(x => x?.LatencyMilliseconds),
                };
                await _iotmqBridge.PublishMessageAsync(itemWithRawTs.ToUtf8Bytes(), TopicsDefinition.ItemsProcessed());
            }
        }

        private async Task PublishLatenciesToBridge(List<ItemTransitionLatency> itemLatencies)
        {
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
        }

        private bool TimestampIsConveyor(string timestampName)
        {
            return Regex.IsMatch(timestampName, @"^[a-zA-Z]{3,}_c\d+(_in|_out)$");
        }

        private bool TimestampIsTransition(string timestampName)
        {
            return Regex.IsMatch(timestampName, @"_to_");
        }

        private bool TransitionTargetIsConveyor(string timestampName)
        {
            return Regex.IsMatch(timestampName, @"_to_[a-zA-Z]{3,}_c\d+$");
        }

        private bool TransitionSourceIsConveyor(string timestampName)
        {
            return Regex.IsMatch(timestampName, @"^[a-zA-Z]{3,}_c\d+_to_");
        }

        private bool TransitionIsConveyorChain(string timestampName)
        {
            return Regex.IsMatch(timestampName, @"^[a-zA-Z]{3,}_c\d+_to_[a-zA-Z]{3,}_c\d+$");
        }
        private bool TimestampIsConveyorIn(string timestampName)
        {
            return Regex.IsMatch(timestampName, @"_in$");
        }

        private bool TimestampIsConveyorOut(string timestampName)
        {
            return Regex.IsMatch(timestampName, @"_out$");
        }

        private string GetZone(string timestamp)
        {
            if (TimestampIsConveyor(timestamp) || timestamp.EndsWith("_created") || timestamp.EndsWith("_terminated") || TimestampIsTransition(timestamp))
            {
                return timestamp.Split("_")[0];
            }
            else
            {
                return timestamp;
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
