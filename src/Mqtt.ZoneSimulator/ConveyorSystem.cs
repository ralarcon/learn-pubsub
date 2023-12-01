using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ConveyorSimulator
{
    public class ConveyorSystem
    {
        private readonly ConveyorSimulatorConfig _config;
        private readonly MqttManager _mqttManager;
        private readonly JunctionsSet _junctions;

        private List<Conveyor> _conveyors = new();
        

        public ConveyorSystem(ConveyorSimulatorConfig config, MqttManager mqttManager)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _mqttManager = mqttManager?? throw new ArgumentNullException(nameof(mqttManager));
            _junctions = new JunctionsSet(config, mqttManager, "c_");
            PrepareConveyors();
        }

        public List<Conveyor> Conveyors => _conveyors;
        public List<Junction> Junctions => _junctions.Junctions;

        private void PrepareConveyors()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tPreparing {_config.NumConveyors} conveyors; Transit Delay: {_config.ConveyorTransitMilliseconds}; Junction Delay: {_config.JunctionDelayMilliseconds}.");
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
            for (int junctionId = 0; junctionId < _conveyors.Count - 1; junctionId++)
            {
                var junction = _junctions.CreateJunction(_conveyors[junctionId].OutTopic, _conveyors[junctionId + 1].InTopic);
            }
        }

        public async Task StartSimulationAsync()
        {
            await _junctions.StartSimulationAsync();
            foreach (var conveyor in _conveyors)
            {
                await conveyor.StartAsync();
            }
        }
    }
}
