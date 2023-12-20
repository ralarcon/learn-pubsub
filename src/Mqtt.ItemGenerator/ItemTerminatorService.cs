using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mqtt.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.TimeZoneInfo;

namespace Mqtt.ItemGenerator
{
    public class ItemTerminatorService : BackgroundService
    {
        private readonly ILogger<ItemTerminatorService> _logger;
        private readonly ItemGeneratorConfig _config;
        private MqttManager _mqtt = default!;
        private readonly Timer _reportStatusTimer;
        private int _currentCount;

        public ItemTerminatorService(ItemGeneratorConfig config, ILogger<ItemTerminatorService> logger)
        {
            _logger = logger;
            _config = config;
            _reportStatusTimer = PrepareReportTimer();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            Console.WriteLine($"[{DateTime.UtcNow}]\tBackground ItemTerminator task is running...");
            if (_config.EnableTermination)
            {
                _mqtt = await MqttManagerFactory.CreateDefault(stoppingToken) ?? throw new ArgumentNullException(nameof(_mqtt));
                Console.WriteLine($"[{DateTime.UtcNow}]\tItem Termination Zone: '{_config.ItemsTermination}'.");
                await StartTerminatingItemsAsync();
            }
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow}]\tItems termination is disabled.");
            }
        }
        private async Task StartTerminatingItemsAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tReady to remove items from zone '{_config.ItemsTermination}' after {_config.TerminationRetentionMilliseconds} seconds of arrival.");

            await _mqtt.SubscribeTopicAsync(TopicsDefinition.Items(_config.ItemsTermination), async (payload) =>
            {
                var timestamp = DateTime.UtcNow;
                if (payload.Array != null)
                {
                    var item = payload.Array.DeserializeItem();
                    if (item != null)
                    {
                        item.Timestamps?.Add($"{_config.ItemsTermination}_terminated".ToLower(), timestamp);
                        item.ItemStatus = ItemStatusEnum.Delivered;
                        await _mqtt.PublishMessageAsync(item.ToItemBytes(), TopicsDefinition.ItemsTerminated());
                        _currentCount++;

                        await UpdateStatusToDestination(item);
                    }
                }
            });
        }


        private async Task UpdateStatusToDestination(Item item)
        {
            //Delay a bit to mark the status to avoid colisions
            await Task.Delay(500);

            //Update item position to destination
            ItemPosition itemPosition = new()
            {
                Id = item.Id,
                BatchId = item.BatchId,
                Zone = _config.ItemsTermination,
                Position = "Destination",
                Status = item.ItemStatus,
                TimeStamp = DateTime.UtcNow
            };
            await _mqtt.PublishStatusAsync(itemPosition.ToItemPositionBytes(), TopicsDefinition.ItemStatus(item.Id));
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
