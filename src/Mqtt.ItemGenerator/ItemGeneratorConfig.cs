using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ItemGenerator
{
    internal class ItemGeneratorConfig
    {
        public int FrequencyMilliseconds { get; set; } = 1000; //Default 1 generation per second
        public int MaxItems { get; set; } = 0; //Zero or negative == infinite
        public string GenerationZone { get; set; } = default!;
        public string TerminationZone { get; set; } = default!;
        public int SimulationStartDelayMilliseconds { get; set; } = 5000;
        public bool EnableTermination { get; set; } = true;
        public bool EnableGeneration { get; set; } = true;
    }
}
