using System.Text.Json.Serialization;

namespace Mqtt.ItemGenerator
{
    public enum ItemTransitionTypeEnum
    {
        Creation,
        ZoneEnter,
        ZoneExit,
        ConveyorEnter,
        ConveyorChain,
        ConveyorTransport,
        Termination,
        Unknown
    }
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
        public string SourceZone { get; set; }
        public string TargetZone { get; set; }
        public string TimestampSourceName { get; set; }
        public string TimestampTargetName { get; set; }
        public DateTime TimestampSource { get; set; }
        public DateTime TimestampTarget { get; set; }
    }
}