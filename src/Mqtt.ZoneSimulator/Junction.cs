using Mqtt.Shared;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ConveyorSimulator
{

    public class JunctionsSet
    {
        private readonly ConveyorSimulatorConfig _config;
        private readonly MqttManager _mqttManager;
        private readonly string _prefix;
        private readonly List<Junction> _junctions = new();

        public JunctionsSet(ConveyorSimulatorConfig config, MqttManager mqttManager, string prefix = "")
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _mqttManager = mqttManager ?? throw new ArgumentNullException(nameof(mqttManager));
            _prefix = prefix;
        }

        public Junction CreateJunction(string sourceTopic, string destinationTopic)
        {
            var junction = new Junction(_junctions.Count+1, $"{_config.Zone}", sourceTopic, destinationTopic, _mqttManager, _config.JunctionDelayMilliseconds, _prefix);
            _junctions.Add(junction);
            return junction;
        }

        public List<Junction> Junctions => _junctions;

        public async Task StartSimulationAsync()
        {
            foreach (var junction in _junctions)
            {
                await junction.ConnectAsync();
            }
        }
    }

    public class Junction
    {
        public readonly string _id;
        public readonly string _zone;
        public readonly string _sourceTopic;
        public readonly string _destinationTopic;
        public readonly int _interconnectionDelayMilliseconds;
        public readonly MqttManager _mqttManager;
        private readonly Timer _reportJunctionStatus;
        private int _transits = 0;
        private bool _connected = false;

        public Junction(int instance, string zone, string sourceTopic,  string destinationTopic, MqttManager mqttManager, int interconnectionDelayMilliseconds=0, string idPrefix = "")
        {
            _id = $"{idPrefix}{zone}_j{instance}";
            _zone = zone;
            _sourceTopic = sourceTopic;
            _destinationTopic = destinationTopic;
            _mqttManager = mqttManager;
            _interconnectionDelayMilliseconds = interconnectionDelayMilliseconds;
            _reportJunctionStatus = PrepareReportTimer();
        }
        public string Id => _id;
        public string SourceTopic => _sourceTopic;
        public string DestinationTopic => _destinationTopic;
        public int Delay => _interconnectionDelayMilliseconds;
        public int Transits => _transits;
        public bool Connected => _connected;

        public async Task ConnectAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tConnecting conveyor '{_sourceTopic}' to conveyor '{_destinationTopic}' with a delay of {_interconnectionDelayMilliseconds}.");
            await _mqttManager.SubscribeTopicAsync(_sourceTopic, async (payload) =>
            {
                if (payload.Array != null)
                {
                    await Task.Delay(_interconnectionDelayMilliseconds);

                    _transits++;

                    Item item = await payload.Array.DeserializeAsync<Item>();
                    item.Timestamps?.Add(_id, DateTime.UtcNow);
                    await _mqttManager.PublishMessageAsync(await item.ToJsonByteArrayAsync(), _destinationTopic);

                    await ReportZoneTransitionIfNeeded(_sourceTopic, _destinationTopic, payload);
                }
            });
            _connected = true;
        }

        private async Task ReportZoneTransitionIfNeeded(string sourceTopic, string destinationTopic, ArraySegment<byte> payload)
        {
            if (payload.Array != null)
            {
                string sourceZone = TopicsDefinition.GetZone(sourceTopic); 
                string destinationZone = TopicsDefinition.GetZone(destinationTopic);
                if (!sourceZone.Equals(destinationZone))
                {
                    Item item = await payload.Array.DeserializeAsync<Item>();
                    ItemPosition itemPosition = new()
                    {
                        Id = item.Id,
                        BatchId = item.BatchId,
                        Zone = sourceTopic,
                        Position = $"Transition to {destinationZone}",
                        Status = item.ItemStatus,
                        TimeStamp = DateTime.UtcNow
                    };
                    await _mqttManager.PublishStatusAsync(await itemPosition.ToJsonByteArrayAsync(), TopicsDefinition.ItemStatus(item.Id));
                }
            }
        }

        public async Task DisconnectAsync()
        {
            await _mqttManager.UnsubscribeTopicAsync(_sourceTopic);
            _connected = false;
        }

        private Timer PrepareReportTimer()
        {
            return new Timer(async (data) =>
            {
                var instance = data as Junction;
                if (instance != null)
                {
                    var status = new { SourceTopic=instance.SourceTopic, DestinationTopic=instance.DestinationTopic, Transits = instance.Transits, Connected = instance.Connected, Timestamp = DateTime.UtcNow };
                    await _mqttManager.PublishStatusAsync(await status.ToJsonByteArrayAsync(), TopicsDefinition.JunctionStatus(_zone, instance.Id));
                }
            }, this, 15000, 10000);
        }
    }
}
