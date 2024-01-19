using Mqtt.Shared;
using MQTTnet.Client;
using MQTTnet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Mqtt.ItemGenerator;

namespace Mqtt.ZoneSimulator
{
    public enum ConveyorSensor
    {
        In,
        Out
    }
    public class Conveyor
    {
        private readonly string _id;
        private readonly MqttManager _mqttManager;
        private readonly int _transitDelayMilliseconds;
        private readonly int _interconnectionDelayMilliseconds = 0;
        private readonly string _zone;
        private readonly Timer _reportConveyorStatus;

        private int _itemsIn = 0;
        private int _itemsOut = 0;

        List<Timer> _cretedTimers = new List<Timer>();

        public Conveyor(int instanceId, MqttManager mqttManager, string zone, int transitDelayMilliseconds, int interconnectionDelayMilliseconds)
        {
            
            _mqttManager = mqttManager;
            _zone = zone;
            _id = $"{_zone}_c{instanceId}";
            _transitDelayMilliseconds = transitDelayMilliseconds;
            _interconnectionDelayMilliseconds = interconnectionDelayMilliseconds;

            
            _reportConveyorStatus = PrepareReportTimer();
        }

        public string Id => _id;
        public string InTopic { get { return TopicsDefinition.ConveyorSensor(_zone, _id, nameof(ConveyorSensor.In)); } }
        public string OutTopic { get { return TopicsDefinition.ConveyorSensor(_zone, _id, nameof(ConveyorSensor.Out)); } } 
        public int ItemsIn => _itemsIn; 
        public int ItemsOut => _itemsOut;
        public int ItemsInTransit => _itemsIn - ItemsOut;

        public async Task StartAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tStarting Conveyor with Id {_id} waiting for items in the topic '{InTopic}'.");
            Console.WriteLine($"[{DateTime.UtcNow}]\t>> Conveyor: subscribing to {InTopic} topic and publishing to {OutTopic}.");

            await _mqttManager.SubscribeTopicAsync(InTopic, async (payload, sourceTs, receiveTs, hopms) =>
            {
                var array = payload.Array ?? throw new ArgumentNullException(nameof(payload));

                var item = array.DeserializeItem();

                item.ItemStatus = ItemStatusEnum.InTransit;

                await SimulateConveyorEnter(item, sourceTs, receiveTs, hopms).ConfigureAwait(false);

                await SimulateConveyorTransit(item).ConfigureAwait(false);

                await SimulateConveyorExit(item, receiveTs).ConfigureAwait(false);
            });
        }

        public async Task InterConnect(Conveyor nextConveyor)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tInterconnecting in conveyor {_id}[OUT] TO {nextConveyor.Id}[IN].");

            string sourceTopic = TopicsDefinition.ConveyorSensor(_zone, _id, nameof(ConveyorSensor.Out));
            string targetTopic = TopicsDefinition.ConveyorSensor(_zone, nextConveyor.Id, nameof(ConveyorSensor.In));
            
            Console.WriteLine($"[{DateTime.UtcNow}]\t> Interconnection: subscribing to {sourceTopic} topic and publishing to {targetTopic}.");

            await _mqttManager.SubscribeTopicAsync(sourceTopic, async (payload, sourceTs, receiveTs, hopms) =>
            {
                Item item = payload.Array.DeserializeItem();

                await _mqttManager.PublishHopLatency(new ItemTransitionLatency()
                {
                    Id = item.Id,
                    BatchId = item.BatchId,
                    TransitionType = ItemTransitionTypeEnum.ConveyorChain,
                    LatencyMilliseconds = hopms,
                    TimestampSource = sourceTs,
                    TimestampTarget = receiveTs,
                    TimestampTargetName = $"{_id}_to_{nextConveyor.Id}"
                });

                await Task.Delay(_interconnectionDelayMilliseconds).ConfigureAwait(false);

                await _mqttManager.PublishMessageAsync(item.ToItemBytes(), targetTopic).ConfigureAwait(false);
            });
        }

        public async Task ConnectTransitionToAsync(string destinationZone)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tConnecting transition in FROM conveyor {_id} TO {destinationZone}.");
            
            string sourceTopic = TopicsDefinition.ConveyorSensor(_zone, _id, nameof(ConveyorSensor.Out));
            string targetTopic = TopicsDefinition.Items(destinationZone);

            Console.WriteLine($"[{DateTime.UtcNow}]\t# Transition: subscribing to {sourceTopic} topic and publishing to {targetTopic}.");

            await _mqttManager.SubscribeTopicAsync(sourceTopic, async (payload, sourceTs, receiveTs, hopms) =>
            {

                Item item = payload.Array.DeserializeItem();

                await _mqttManager.PublishHopLatency(new ItemTransitionLatency()
                {
                    Id = item.Id,
                    BatchId = item.BatchId,
                    TransitionType = ItemTransitionTypeEnum.ZoneTransitionTo,
                    LatencyMilliseconds = hopms,
                    TimestampSource = sourceTs,
                    TimestampTarget = receiveTs,
                    TimestampTargetName = $"{_id}_to_{destinationZone}"
                });

                await Task.Delay(_interconnectionDelayMilliseconds).ConfigureAwait(false);

                await _mqttManager.PublishMessageAsync(item.ToItemBytes(), targetTopic).ConfigureAwait(false);

                await ReportItemZoneTransition(item, _id, destinationZone).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async Task ConnectTransitionFromAsync(string sourceZone)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tConnecting transition in FROM {sourceZone} TO conveyor {_id}.");
            
            string sourceTopic = TopicsDefinition.Items(sourceZone);
            string targetTopic = TopicsDefinition.ConveyorSensor(_zone, _id, nameof(ConveyorSensor.In));

            Console.WriteLine($"[{DateTime.UtcNow}]\t# Transition: subscribing to {sourceTopic} topic and publishing to {targetTopic}.");

            await _mqttManager.SubscribeTopicAsync(sourceTopic, async (payload, sourceTs, receiveTs, hopms) =>
            {
                Item item = payload.Array.DeserializeItem();

                await _mqttManager.PublishHopLatency(new ItemTransitionLatency()
                {
                    Id = item.Id,
                    BatchId = item.BatchId,
                    TransitionType = ItemTransitionTypeEnum.ZoneTransitionFrom,
                    LatencyMilliseconds = hopms,
                    TimestampSource = sourceTs,
                    TimestampTarget = receiveTs,
                    TimestampTargetName = $"{sourceZone}_to_{_id}"
                });

                await Task.Delay(_interconnectionDelayMilliseconds).ConfigureAwait(false);
                
                await _mqttManager.PublishMessageAsync(item.ToItemBytes(), targetTopic).ConfigureAwait(false);

                await ReportItemZoneTransition(item, sourceZone, _id).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }


        private async Task SimulateConveyorEnter(Item item, DateTime sourceTs, DateTime receiveTs, double hopms)
        {

            await _mqttManager.PublishHopLatency(new ItemTransitionLatency()
            {
                Id = item.Id,
                BatchId = item.BatchId,
                TransitionType = ItemTransitionTypeEnum.ConveyorEnter,
                LatencyMilliseconds = hopms,
                TimestampSource = sourceTs,
                TimestampTarget = receiveTs,
                TimestampTargetName = $"{_id}_{nameof(ConveyorSensor.In)}".ToLower()
            });

            _itemsIn++;


            await ReportItemPositionAsync(item, ConveyorSensor.In).ConfigureAwait(false);
        }

        private async Task SimulateConveyorTransit(Item item)
        {
            await Task.Delay(_transitDelayMilliseconds).ConfigureAwait(false);
        }
        
        private async Task SimulateConveyorExit(Item item, DateTime receiveTs)
        {

            await _mqttManager.PublishHopLatency(new ItemTransitionLatency()
            {
                Id = item.Id,
                BatchId = item.BatchId,
                TransitionType = ItemTransitionTypeEnum.ConveyorExit,
                LatencyMilliseconds = (DateTime.UtcNow - receiveTs).TotalMilliseconds,
                TimestampSource = receiveTs,
                TimestampTarget = DateTime.UtcNow,
                TimestampTargetName = $"{_id}_{nameof(ConveyorSensor.Out)}".ToLower()
            });


            await ReportItemPositionAsync(item, ConveyorSensor.Out).ConfigureAwait(false);
            _itemsOut++;

            await _mqttManager.PublishMessageAsync(item.ToItemBytes(), OutTopic).ConfigureAwait(false);
        }

        private async Task ReportItemPositionAsync(Item item, ConveyorSensor sensor)
        {
            ItemPosition itemPosition = new()
            {
                Id = item.Id,
                Zone = _zone,
                BatchId = item.BatchId,
                Position = $"Conveyor {_id} sensor {Enum.GetName(typeof(ConveyorSensor), sensor)!}",
                TimeStamp = DateTime.UtcNow,
                Status = item.ItemStatus
            };
            await _mqttManager.PublishStatusAsync(itemPosition.ToItemPositionBytes(), TopicsDefinition.ItemStatus(item.Id)).ConfigureAwait(false);
        }

        private async Task ReportItemZoneTransition(Item item, string from, string to)
        {
            ItemPosition itemPosition = new()
            {
                Id = item.Id,
                BatchId = item.BatchId,
                Zone = _zone,
                Position = $"Transition from {from} to {to}",
                Status = item.ItemStatus,
                TimeStamp = DateTime.UtcNow
            };
            await _mqttManager.PublishStatusAsync(itemPosition.ToItemPositionBytes(), TopicsDefinition.ItemStatus(item.Id)).ConfigureAwait(false);
        }


        private Timer PrepareReportTimer()
        {
            return new Timer(async (data) =>
            {
                var instance = data as Conveyor;
                if (instance != null)
                {
                    var status = new { ItemsIn = instance.ItemsIn, ItemsInTransit = instance.ItemsInTransit, ItemsOut = instance.ItemsOut, Timestamp = DateTime.UtcNow, In = instance.InTopic, Out = instance.OutTopic};
                    await _mqttManager.PublishStatusAsync(status.ToUtf8Bytes(), TopicsDefinition.ConveyorStatus(_zone, _id)).ConfigureAwait(false);
                }
            }, this, 15000, 10000);
        }
    }
}
