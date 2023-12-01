using Mqtt.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ItemGenerator
{
    internal class ItemTerminator
    {
        private readonly ItemGeneratorConfig _config;
        private readonly MqttManager _mqtt;
        private readonly Timer _reportStatusTimer;
        private int _currentCount;

        public ItemTerminator(ItemGeneratorConfig config, MqttManager mqtt)
        {
            _config = config;
            _mqtt = mqtt;
            _reportStatusTimer = PrepareReportTimer();
        }


        public async Task StartTerminatingItems()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tReady to remove items from zone '{_config.TerminationZone}' after 30 seconds of arrival.");
            await _mqtt.SubscribeTopicAsync(TopicsDefinition.Items(_config.TerminationZone), async (payload) =>
            {
                if (payload.Array != null)
                {
                    var item = await payload.Array.DeserializeAsync<Item>();
                    if (item != null)
                    {
                        await Task.Delay(30000);
                        await _mqtt.PublishStatusAsync(new byte[] { }, TopicsDefinition.ItemStatus(item.Id));
                        _currentCount++;
                    }
                }
            });
        }

        private Timer PrepareReportTimer()
        {
            return new Timer((state) =>
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\t{_currentCount} items terminated.");
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }
    }
}
