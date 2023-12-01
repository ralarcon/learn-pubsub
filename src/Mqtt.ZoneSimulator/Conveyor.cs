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

namespace Mqtt.ConveyorSimulator
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
        private readonly ConveyorSimulatorConfig _config;
        private readonly Timer _reportConveyorStatus;
        private int _itemsIn = 0;
        private int _itemsOut = 0;
        

        List<Timer> _cretedTimers = new List<Timer>();

        public Conveyor(int instanceId, MqttManager mqttManager, ConveyorSimulatorConfig config)
        {
            _id = $"c{instanceId}";
            _mqttManager = mqttManager;
            _config = config;
            _reportConveyorStatus = PrepareReportTimer();
        }

        public string Id => _id;
        public string InTopic => TopicsDefinition.ConveyorSensor(_config.Zone, _id, nameof(ConveyorSensor.In));
        public string OutTopic => TopicsDefinition.ConveyorSensor(_config.Zone, _id, nameof(ConveyorSensor.Out));
        public int ItemsIn => _itemsIn; 
        public int ItemsOut => _itemsOut;
        public int ItemsInTransit => _itemsIn - ItemsOut;

        public async Task StartAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tStarting Conveyor with Id {_id} waiting for items in the topic '{InTopic}'.");
            await _mqttManager.SubscribeTopicAsync(InTopic, ItemDetectedHandler);
        }

        private async Task ItemDetectedHandler(ArraySegment<byte> payload)
        {
            var array = payload.Array ?? throw new ArgumentNullException(nameof(payload));
            var item = await array.DeserializeAsync<Item>();
            
            item.ItemStatus = ItemStatusEnum.InTransit;

            await SimulateConveyorEnter(item);

            await SimulateConveyorTransit(item);

            await SimulateConveyorExit(item);
        }

        private async Task SimulateConveyorEnter(Item item)
        {
            item.Timestamps?.Add($"{_config.Zone}_{_id}_{nameof(ConveyorSensor.In)}".ToLower(), DateTime.UtcNow);
            _itemsIn++;
            await ReportItemPositionAsync(item, ConveyorSensor.In);
        }

        private async Task SimulateConveyorTransit(Item item)
        {
            await Task.Delay(_config.ConveyorTransitMilliseconds);
        }
        
        private async Task SimulateConveyorExit(Item item)
        {
            item.Timestamps?.Add($"{_config.Zone}_{_id}_{nameof(ConveyorSensor.Out)}".ToLower(), DateTime.UtcNow);
            await _mqttManager.PublishMessageAsync(await item.ToJsonByteArrayAsync(), OutTopic);
            _itemsOut++;
            await ReportItemPositionAsync(item, ConveyorSensor.Out);
        }

        private async Task ReportItemPositionAsync(Item item, ConveyorSensor sensor)
        {
            ItemPosition itemPosition = new()
            {
                Id = item.Id,
                Zone = _config.Zone,
                BatchId = item.BatchId,
                Position = $"Conveyor {_id} sensor {Enum.GetName(typeof(ConveyorSensor), sensor)!}",
                TimeStamp = DateTime.UtcNow,
                Status = item.ItemStatus
            };
            await _mqttManager.PublishStatusAsync(await itemPosition.ToJsonByteArrayAsync(), TopicsDefinition.ItemStatus(item.Id));
        }

        private Timer PrepareReportTimer()
        {
            return new Timer(async (data) =>
            {
                var instance = data as Conveyor;
                if (instance != null)
                {
                    var status = new { ItemsIn = instance.ItemsIn, ItemsInTransit = instance.ItemsInTransit, ItemsOut = instance.ItemsOut, Timestamp = DateTime.UtcNow };
                    await _mqttManager.PublishStatusAsync(await status.ToJsonByteArrayAsync(), TopicsDefinition.ConveyorStatus(_config.Zone, _id));
                }
            }, this, 15000, 10000);
        }
    }
}
