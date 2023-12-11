using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.Shared
{
    public class ItemLatencies
    {
        public int Id { get; set; }
        public Guid BatchId { get; set; }
        public Dictionary<string, TimeSpan>? Latencies { get; set; }
        public Dictionary<string, DateTime>? Timestamps { get; set; }
    }
}
