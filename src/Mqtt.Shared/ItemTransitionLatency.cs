using System.Text.Json.Serialization;

namespace Mqtt.ItemGenerator
{
    public enum ItemTransitionTypeEnum
    {
        Creation,
        ZoneTransitionTo,
        ZoneTransitionFrom,
        ConveyorEnter,
        ConveyorExit,
        ConveyorChain,
        ConveyorTransport,
        Termination,
        Unknown
    }
    //public class ItemTransitionLatency
    //{
    //    public ItemTransitionLatency()
    //    {
    //    }

    //    public int Id { get; set; }
    //    public Guid BatchId { get; set; }
    //    [JsonConverter(typeof(JsonStringEnumConverter))]
    //    public ItemTransitionTypeEnum TransitionType { get; set; }
    //    public double LatencyMilliseconds { get; set; }
    //    public string SourceZone { get; set; } = default!;
    //    public string TargetZone { get; set; } = default!;
    //    public string TimestampSourceName { get; set; } = default!;
    //    public string TimestampTargetName { get; set; } = default!;
    //    public DateTime TimestampSource { get; set; }
    //    public DateTime TimestampTarget { get; set; }
    //}

    public class ItemTransitionLatency
    {
        public ItemTransitionLatency()
        {
        }

        public int Id { get; set; }
        public Guid BatchId { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ItemTransitionTypeEnum TransitionType { get; set; }
        public double LatencyMilliseconds { get; set; }
        public string TimestampTargetName { get; set; } = default!;
        public DateTime TimestampSource { get; set; }
        public DateTime TimestampTarget { get; set; }
    }

}