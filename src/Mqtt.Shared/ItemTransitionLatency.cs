using System.Text.Json.Serialization;

namespace Mqtt.ItemGenerator
{
    public enum ItemTransitionTypeEnum
    {
        ZoneEnter,
        ZoneExit,
        ConveyorEnter,
        ConveyorChain,
        ConveyorTransport,
        Unknown,
        Termination
    }
    public class ItemTransitionLatency
    {
        public ItemTransitionLatency()
        {
        }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ItemTransitionTypeEnum TransitionType { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public TimeSpan Latency { get; set; }
        public string SourceZone { get; set; }
        public string TargetZone { get; set; }
        public int Id { get; set; }
        public Guid BatchId { get; set; }
        public DateTime TimestampSource { get; set; }
        public DateTime TimestampTarget { get; set; }
    }
}