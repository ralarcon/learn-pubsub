using Mqtt.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ZoneSimulator
{
    public class ConveyorSystem
    {
        private readonly ZoneSimulatorConfig _config;
        private readonly MqttManager _mqttManager;
        private readonly string _sourceZone;
        private readonly string _destinationZone;

        private List<Conveyor> _conveyors = new();
        

        public ConveyorSystem(ZoneSimulatorConfig config, MqttManager mqttManager, string sourceZone, string destinationZone)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _mqttManager = mqttManager?? throw new ArgumentNullException(nameof(mqttManager));
            _sourceZone = sourceZone ?? throw new ArgumentNullException(nameof(sourceZone));
            _destinationZone = destinationZone ?? throw new ArgumentNullException(nameof(destinationZone));
        }

        public List<Conveyor> Conveyors => _conveyors;

        public async Task PrepareConveyors()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tPreparing {_config.NumConveyors} conveyors; Transit Delay: {_config.ConveyorTransitMilliseconds}; Interconnection Delay: {_config.InterconectionDelayMilliseconds}.");
            CreateConveyors();
            await ConnectConveyors();
        }

        public async Task StartSimulationAsync()
        {
            foreach (var conveyor in _conveyors)
            {
                await conveyor.StartAsync();
            }
        }

        private void CreateConveyors()
        {

            _conveyors.Clear();

            //Create Conveyors
            for (int conveyorId = 1; conveyorId <= _config.NumConveyors; conveyorId++)
            {
                var conveyor = new Conveyor(conveyorId, _mqttManager, _config.Zone, _config.ConveyorTransitMilliseconds, _config.InterconectionDelayMilliseconds);
                _conveyors.Add(conveyor);
            }
        }

        private async Task ConnectConveyors()
        {
            //Connect First Conveyor to source zone
            int firstConveyorIndex = 0;
            await _conveyors[firstConveyorIndex].ConnectTransitionFromAsync(_sourceZone);

            //Chain conveyors
            for (int i = 0; i < _conveyors.Count-1; i++)
            {
                await _conveyors[i].InterConnect(_conveyors[i+1]);
            }

            //Connect last conveyor to destination zone
            int lastConveyorIndex = _conveyors.Count - 1;
            await _conveyors[lastConveyorIndex].ConnectTransitionToAsync(_destinationZone);
        }


    }
}
