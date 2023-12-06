using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

public class MqttConfig
{
    public string MqttServer { get; set; } = default!;
    public int MqttPort { get; set; }
    public int QoS { get; set; } = 1;
    public string ClientId { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public bool TrackStatus { get; set; } = true!;

}

