using Microsoft.Extensions.Hosting;
using Mqtt.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mqtt.ItemGenerator
{
    public class ItemStatsService : BackgroundService
    {
        private readonly ItemGeneratorConfig _config;
        private readonly MqttManager _mqtt;
        private readonly MqttManager _iotmqBridge;
        private readonly Timer _reportStatusTimer;
        private int _currentCount;
        private readonly ConcurrentDictionary<int, Timer> _removalTimers;
        private readonly ConcurrentQueue<Item> _terminateItems = new ConcurrentQueue<Item>();

        public ItemStatsService(ItemGeneratorConfig config, MqttManager mqtt, MqttManager iotmqBridge)
        {
            _config = config;
            _mqtt = mqtt;
            _iotmqBridge = iotmqBridge;
            _reportStatusTimer = PrepareReportTimer();
            _removalTimers = new ConcurrentDictionary<int, Timer>();
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            Console.WriteLine("Background ItemStatsProcessor task is running...");
            if (_config.EnableTermination)
            {
                await StartGatheringTerminatedItemStats();
            }
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItems termination is disabled no stats are being gathered.");
            }
        }
        private async Task StartGatheringTerminatedItemStats()
        {
            await _mqtt.SubscribeTopicAsync(TopicsDefinition.ItemsTerminated(), async (payload) =>
            {
                if (payload.Array != null)
                {
                    var item = payload.Array.DeserializeItem();
                    if (item != null)
                    {
                        await CalulateAndPublishLatenciesAsync(item);

                        //await RemoveItemFromStatusAsync(item);
                        ProgramItemRemovalFromStatus(item);
                    }
                }
            });
        }

        private async Task RemoveItemFromStatusAsync(Item item)
        {
            await Task.Delay(_config.TerminationRetentionMilliseconds - 500);
            //Remove Item from status
            await _mqtt.RemoveStatusAsync(TopicsDefinition.ItemStatus(item.Id));
        }


        private void ProgramItemRemovalFromStatus(Item item)
        {
            //Execute removal after _config.TerminationRetentionMilliseconds
            Timer timer = new Timer(async (state) =>
            {
                if (!_removalTimers.TryRemove(item.Id, out var bogus))
                {
                    Console.WriteLine($"[{DateTime.UtcNow}]\tItem {item.Id} removal timer not found.");
                }
                await _mqtt.RemoveStatusAsync(TopicsDefinition.ItemStatus(item.Id)).ConfigureAwait(false);
            }, null, _config.TerminationRetentionMilliseconds, Timeout.Infinite);

            _removalTimers.TryAdd(item.Id, timer);
        }

        private async Task CalulateAndPublishLatenciesAsync(Item item)
        {
            List<ItemTransitionLatency> itemLatencies = new();

            if (item.Timestamps?.Count > 0)
            {
                for (int i = 0; i < item.Timestamps.Count - 1; i++)
                {
                    var sourceTimestamp = item.Timestamps.ElementAt(i).Key;
                    var targetTimestamp = item.Timestamps.ElementAt(i + 1).Key;

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
                        TimestampSource = item.Timestamps.ElementAt(i).Value,
                        TimestampTarget = item.Timestamps.ElementAt(i + 1).Value,
                        LatencyMilliseconds = (item.Timestamps.ElementAt(i + 1).Value - item.Timestamps.ElementAt(i).Value).TotalMilliseconds
                    });
                }
                await PublishLatenciesToBridge(itemLatencies).ConfigureAwait(false);

                await PublishItemDetailsToBridge(item, itemLatencies).ConfigureAwait(false);
            }
        }


        private ItemTransitionTypeEnum GetTransitionType(string source, string target)
        {
            var transitionType = ItemTransitionTypeEnum.Unknown;
            if (source.EndsWith("_created"))
            {
                transitionType = ItemTransitionTypeEnum.Creation;
            }
            else if (target.EndsWith("_terminated"))
            {
                transitionType = ItemTransitionTypeEnum.Termination;
            }
            else if (TimestampIsTransition(target))
            {
                if (!TransitionIsConveyorChain(source) && TransitionSourceIsConveyor(source) && !TransitionIsConveyorChain(target) && TransitionTargetIsConveyor(target))
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
            if (item == null || item.Timestamps == null)
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
                await _iotmqBridge.PublishMessageAsync(itemWithRawTs.ToUtf8Bytes(), TopicsDefinition.ItemsProcessed()).ConfigureAwait(false);
            }
        }

        private async Task PublishLatenciesToBridge(List<ItemTransitionLatency> itemLatencies)
        {
            foreach (var itemLatency in itemLatencies)
            {
                if (_config.EnableBridgeToIoTMQ)
                {
                    await _iotmqBridge.PublishMessageAsync(itemLatency.ToUtf8Bytes(), TopicsDefinition.ItemsLatencies()).ConfigureAwait(false);
                }
                else
                {
                    await _mqtt.PublishMessageAsync(itemLatency.ToUtf8Bytes(), TopicsDefinition.ItemsLatencies()).ConfigureAwait(false);
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
                Console.WriteLine($"[{DateTime.UtcNow}]\t{_currentCount} items stats processed.");
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }
    }
}
