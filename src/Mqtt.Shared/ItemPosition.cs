using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Mqtt.Shared
{
    public class ItemPosition
    {
        public int Id { get; set; }
        public ItemStatusEnum Status { get; set; }
        public string Position { get; set; } = default!;
        public DateTime TimeStamp { get; set; }
        public Guid BatchId { get; set; }
        public string Zone { get; set; } = default!;
    }

    [JsonSerializable(typeof(ItemPosition))]
    internal partial class ItemPositionJsonContext : JsonSerializerContext
    {
    }
}
