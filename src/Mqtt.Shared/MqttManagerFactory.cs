using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.Shared
{
    public class MqttManagerFactory
    {
        static readonly MqttConfig _defaultBrokerConfig;
        static MqttManager _defaultMqttManager = default!;
        static MqttManagerFactory()
        {
            _defaultBrokerConfig = AppConfigProvider.LoadConfiguration<MqttConfig>();
        }

        static async public Task<MqttManager> CreateDefault(CancellationToken cancellationToken)
        {
            if (_defaultMqttManager != null)
            {
                return _defaultMqttManager;
            }
            else
            {
                _defaultMqttManager = new MqttManager(_defaultBrokerConfig, cancellationToken);
                await _defaultMqttManager.StartMqttClientAsync();
                return _defaultMqttManager;
            }
        }
    }
}
