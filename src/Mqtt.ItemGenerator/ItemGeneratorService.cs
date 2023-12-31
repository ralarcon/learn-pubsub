﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mqtt.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mqtt.ItemGenerator
{
    public class ItemGeneratorService : BackgroundService
    {
        private readonly ILogger<ItemGeneratorService> _logger;
        private readonly ItemGeneratorConfig _config;
        private readonly MqttManager _mqtt;
        private readonly Timer _reportStatusTimer;
        private int _currentCount;

        public ItemGeneratorService(ItemGeneratorConfig config, MqttManager mqtt, ILogger<ItemGeneratorService> logger) 
        {
            _logger = logger;
            _config = config;
            _mqtt = mqtt;
            _reportStatusTimer = AliveReportTimer();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            Console.WriteLine("Background ItemGenerator task is running...");
            if (_config.EnableGeneration)
            {
                if (_config.SimulationStartDelayMilliseconds > 0)
                {
                    Console.WriteLine($"[{DateTime.UtcNow}]\tItem Generation start delayed by {_config.SimulationStartDelayMilliseconds} milliseconds...");
                    await Task.Delay(_config.SimulationStartDelayMilliseconds);
                }
                await StartGeneratingItems(stoppingToken);
            }
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItems generation is disabled.");
            }
        }

        private async Task StartGeneratingItems(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tItems generating started. Frequency: {_config.FrequencyMilliseconds} ms; MaxItems: {_config.MaxItems}.");
            _currentCount = 0;


            Guid batchId = Guid.NewGuid();

            while ((_config.MaxItems == 0 || _currentCount < _config.MaxItems) && !cancellationToken.IsCancellationRequested)
            {
                _currentCount++;
                Item item = new()
                {
                    Id = _currentCount,
                    BatchId = batchId,
                    Timestamps = new Dictionary<string, DateTime>()
                    {
                        { $"{_config.ItemsGeneration}_created", DateTime.UtcNow }
                    }
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
            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItems generation stopped. Cancellation is requested.");
            }
            if (_currentCount >= _config.MaxItems)
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItems generation stopped. Maximum items reached.");
            }
        }

        private Timer AliveReportTimer()
        {
            return new Timer((state) =>
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\t{_currentCount} items generated.");
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }
    }
}
