using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ConveyorSimulator
{
    public class ConveyorSimulatorConfig
    {
        public string Zone { get; set; } = default!;
        public string SourceZone { get; set; } = default!;
        public string DestinationZone { get; set; } = default!;
        public int NumConveyors { get; set; }
        public int ConveyorTransitMilliseconds { get; set; } = 5000;
        public int JunctionDelayMilliseconds { get; set; } = 15;
        
    }
}
