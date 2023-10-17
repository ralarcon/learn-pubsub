using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class EchoMessage
{
    public string ClientId { get; set; } = default!;
    public string SourceTopic { get; set; } = default!;
    public string DestinationTopic { get; set; } = default!;
    public DateTime ReceiveTimestamp { get; set; } = DateTime.UtcNow;
    public DateTime EchoTimestamp { get; set; } = DateTime.UtcNow;
    public Message OriginalMessage { get; set; } = default!;
    public TimeSpan SourceToEchoDiff { get; set; }
    public int Id { get; set; }
}