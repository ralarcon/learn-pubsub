using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ZoneSimulator
{
    public class ConveyorSystem
    {
        private readonly ZoneSimulatorConfig _config;
        private readonly MqttManager _mqttManager;
        private readonly PositionSet _positions;

        private List<Conveyor> _conveyors = new();
        

        public ConveyorSystem(ZoneSimulatorConfig config, MqttManager mqttManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _mqttManager = mqttManager?? throw new ArgumentNullException(nameof(mqttManager));
            _positions = new PositionSet(config, mqttManager);
            PrepareConveyors();
        }

        public List<Conveyor> Conveyors => _conveyors;
        public List<Position> Positions => _positions.Positions;

        private void PrepareConveyors()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tPreparing {_config.NumConveyors} conveyors; Transit Delay: {_config.ConveyorTransitMilliseconds}; Position Delay: {_config.PositionDelayMilliseconds}.");
            CreateConveyors();
            ConnectConveyors();
        }

        private void CreateConveyors()
        {
            _conveyors.Clear();

            for (int conveyorId = 1; conveyorId <= _config.NumConveyors; conveyorId++)
            {
                var conveyor = new Conveyor(conveyorId, _mqttManager, _config);
                _conveyors.Add(conveyor);
            }
        }

        private void ConnectConveyors()
        {
            for (int positionId = 0; positionId < _conveyors.Count - 1; positionId++)
            {
                var position = _positions.CreatePosition(_conveyors[positionId].OutTopic, _conveyors[positionId + 1].InTopic, _conveyors[positionId].Id, _conveyors[positionId+1].Id);
            }
        }

        public async Task StartSimulationAsync()
        {
            await _positions.StartSimulationAsync();
            foreach (var conveyor in _conveyors)
            {
                await conveyor.StartAsync();
            }
        }
    }
}
