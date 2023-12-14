using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ZoneSimulator
{
    public class ZoneSimulatorConfig
    {
        public string Zone { get; set; } = default!;
        public string ItemsSource { get; set; } = default!;
        public string ItemsDestination { get; set; } = default!;
        public int NumConveyors { get; set; }
        public int ConveyorTransitMilliseconds { get; set; } = 5000;
        public int InterconectionDelayMilliseconds { get; set; } = 0;
        
    }
}
