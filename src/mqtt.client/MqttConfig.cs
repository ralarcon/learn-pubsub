using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class MqttConfig
{
    public string MqttServer { get; set; }
    public int MqttPort { get; set; }
    public string EchoTopic { get; set; }
    public string SubscribeTopic { get; set; }
    public string PublishTopic { get; set; }
    public string ClientId { get; set; }
}

