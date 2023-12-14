using Mqtt.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ItemGenerator
{
    internal class ItemGenerator
    {
        private readonly ItemGeneratorConfig _config;
        private readonly MqttManager _mqtt;
        private readonly CancellationToken _cancellationToken;
        private readonly Timer _reportStatusTimer;
        private int _currentCount;

        public ItemGenerator(ItemGeneratorConfig config, MqttManager mqtt, CancellationToken cancellationToken)
        {
            _config = config;
            _mqtt = mqtt;
            _cancellationToken = cancellationToken;
            _reportStatusTimer = PrepareReportTimer();
        }


        public async Task StartGeneratingItems()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tItems generating started. Frequency: {_config.FrequencyMilliseconds} ms; MaxItems: {_config.MaxItems}.");
            _currentCount = 0;

            await _mqtt.PublishMessageAsync((new Item()).ToItemBytes(), TopicsDefinition.Items(_config.ItemsGeneration));

            while ((_config.MaxItems == 0 || _currentCount < _config.MaxItems) && !_cancellationToken.IsCancellationRequested)
            {
                _currentCount++;
                Item item = new()
                {
                    Id = _currentCount,
                    BatchId = Guid.NewGuid(),
                    Timestamps = new Dictionary<string, DateTime>()
                    {
                        { $"{_config.ItemsGeneration}_created", DateTime.UtcNow }
                    },
                };
                ItemPosition itemPosition = new()
                {
                    Id = item.Id,
                    BatchId = item.BatchId,
                    Zone = _config.ItemsGeneration,
                    Position = "Origin",
                    Status = item.ItemStatus,
                    TimeStamp = DateTime.UtcNow
                };
                await _mqtt.PublishMessageAsync(item.ToItemBytes(),TopicsDefinition.Items(_config.ItemsGeneration));

                await _mqtt.PublishStatusAsync(itemPosition.ToItemPositionBytes(), TopicsDefinition.ItemStatus(item.Id));

                await Task.Delay(_config.FrequencyMilliseconds);
            }
            if (_cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItems generation stopped. Cancellation is requested.");
            }
            if (_currentCount >= _config.MaxItems)
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItems generation stopped. Maximum items reached.");
            }
        }

        private Timer PrepareReportTimer()
        {
            return new Timer((state) =>
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\t{_currentCount} items generated.");
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }
    }
}
