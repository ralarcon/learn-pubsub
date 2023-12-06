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

namespace Mqtt.ZoneSimulator
{

    public class PositionSet
    {
        private readonly ZoneSimulatorConfig _config;
        private readonly MqttManager _mqttManager;
        private readonly List<Position> _positions = new();

        public PositionSet(ZoneSimulatorConfig config, MqttManager mqttManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _mqttManager = mqttManager ?? throw new ArgumentNullException(nameof(mqttManager));
        }

        public Position CreatePosition(string sourceTopic, string destinationTopic, string sourceName, string destinationName)
        {
            //var position = new Position(_positions.Count+1, $"{_config.Zone}", sourceTopic, destinationTopic, sourceName, destinationName, _mqttManager, _config.PositionDelayMilliseconds);
            var position = new Position($"{_config.Zone}", sourceTopic, destinationTopic, sourceName, destinationName, _mqttManager, _config.PositionDelayMilliseconds);
            _positions.Add(position);
            return position;
        }

        public List<Position> Positions => _positions;

        public async Task StartSimulationAsync()
        {
            foreach (var position in _positions)
            {
                await position.ConnectAsync().ConfigureAwait(false);
            }
        }
    }

    public class Position
    {
        public readonly string _id;
        public readonly string _zone;
        public readonly string _sourceTopic;
        public readonly string _destinationTopic;
        public readonly int _interconnectionDelayMilliseconds;
        public readonly MqttManager _mqttManager;
        private readonly Timer _reportPositionStatus;
        private int _transits = 0;
        private bool _connected = false;

        //public Position(int instance, string zone, string sourceTopic,  string destinationTopic, string sourceName, string destinationName, MqttManager mqttManager, int interconnectionDelayMilliseconds=0)
        public Position(string zone, string sourceTopic, string destinationTopic, string sourceName, string destinationName, MqttManager mqttManager, int interconnectionDelayMilliseconds = 0)
        {
            //_id = $"p{instance}_{sourceName}_to_{destinationName}";
            _id = $"{sourceName}_to_{destinationName}";
            _zone = zone;
            _sourceTopic = sourceTopic;
            _destinationTopic = destinationTopic;
            _mqttManager = mqttManager;
            _interconnectionDelayMilliseconds = interconnectionDelayMilliseconds;
            _reportPositionStatus = PrepareReportTimer();
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
                    await Task.Delay(_interconnectionDelayMilliseconds).ConfigureAwait(false);

                    _transits++;

                    Item item = payload.Array.DeserializeItem();
                    item.Timestamps?.Add(_id, DateTime.UtcNow);
                    await _mqttManager.PublishMessageAsync(item.ToItemBytes(), _destinationTopic).ConfigureAwait(false);

                    await ReportZoneTransitionIfNeeded(_sourceTopic, _destinationTopic, payload).ConfigureAwait(false);
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
                    Item item = payload.Array.DeserializeItem();
                    ItemPosition itemPosition = new()
                    {
                        Id = item.Id,
                        BatchId = item.BatchId,
                        Zone = sourceTopic,
                        Position = $"Transition to {destinationZone}",
                        Status = item.ItemStatus,
                        TimeStamp = DateTime.UtcNow
                    };
                    await _mqttManager.PublishStatusAsync(itemPosition.ToItemPositionBytes(), TopicsDefinition.ItemStatus(item.Id)).ConfigureAwait(false);
                }
            }
        }

        public async Task DisconnectAsync()
        {
            await _mqttManager.UnsubscribeTopicAsync(_sourceTopic).ConfigureAwait(false);
            _connected = false;
        }

        private Timer PrepareReportTimer()
        {
            return new Timer(async (data) =>
            {
                var instance = data as Position;
                if (instance != null)
                {
                    var status = new { SourceTopic=instance.SourceTopic, DestinationTopic=instance.DestinationTopic, Transits = instance.Transits, Connected = instance.Connected, Timestamp = DateTime.UtcNow };
                    await _mqttManager.PublishStatusAsync(status.ToUtf8Bytes(), TopicsDefinition.PositionStatus(_zone, instance.Id)).ConfigureAwait(false);
                }
            }, this, 15000, 10000);
        }
    }
}
