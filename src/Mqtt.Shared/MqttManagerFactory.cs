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
        static readonly MqttConfig _iotmqBrokerConfig;
        static MqttManager _defaultMqttManager = default!;
        static MqttManager _iotmqMqttManager = default!;
        static MqttManagerFactory()
        {
            _defaultBrokerConfig = AppConfigProvider.LoadConfiguration<MqttConfig>();
            _iotmqBrokerConfig = AppConfigProvider.LoadConfiguration<MqttConfig>("IoTMQ");
        }

        static async public Task<MqttManager> CreateDefault(CancellationTokenSource tokenSource)
        {
            if (_defaultMqttManager != null)
            {
                return _defaultMqttManager;
            }
            else
            {
                _defaultMqttManager = new MqttManager(_defaultBrokerConfig, tokenSource);
                await _defaultMqttManager.StartMqttClientAsync();
                return _defaultMqttManager;
            }
        }

        static async public Task<MqttManager> CreateIotmqBridge(CancellationTokenSource tokenSource)
        {
            if(_defaultBrokerConfig.MqttServer != _iotmqBrokerConfig.MqttServer)
            {
                _iotmqMqttManager = new MqttManager(_iotmqBrokerConfig, tokenSource);
                await _iotmqMqttManager.StartMqttClientAsync();
                    return _iotmqMqttManager;
            }
            else
            {
                if(_defaultMqttManager != null)
                {
                    return _defaultMqttManager;
                }
                else
                {
                    _defaultMqttManager = new MqttManager(_defaultBrokerConfig, tokenSource);
                    await _defaultMqttManager.StartMqttClientAsync();
                    return _defaultMqttManager;
                }
            }
        } 
    }
}
