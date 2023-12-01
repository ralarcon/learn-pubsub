using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.Shared
{
    public class MqttManagerFactory
    {
        static async public Task<MqttManager> Create(CancellationTokenSource tokenSource)
        {
           MqttConfig config = AppConfigProvider.LoadConfiguration<MqttConfig>();
           var mqttManager = new MqttManager(config, tokenSource);
                await mqttManager.StartMqttClientAsync();
                return mqttManager;
        }
    }
}
