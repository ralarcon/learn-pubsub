using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class MqttConfig
{
    public string MqttServer { get; set; } = default!;
    public int MqttPort { get; set; }
    public string EchoTopic { get; set; } = default!;
    public string SubscribeTopic { get; set; } = default!;
    public string PublishTopic { get; set; } = default!;
    public string ClientId { get; set; } = default!;
}

