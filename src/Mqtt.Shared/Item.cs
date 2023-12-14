using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Mqtt.Shared
{
    public enum ItemStatusEnum
    {
        Created,
        InTransit,
        Delivered
    }

    public class Item
    {
        public int Id { get; set; }
        public Guid BatchId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ItemStatusEnum ItemStatus { get; set; }
        public Dictionary<string, DateTime>? Timestamps { get; set; }

    }

    [JsonSerializable(typeof(Item))]
    internal partial class ItemJsonContext : JsonSerializerContext
    {
    }
}