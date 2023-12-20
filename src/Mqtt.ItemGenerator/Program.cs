using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mqtt.ItemGenerator;
using Mqtt.Shared;
using System.Collections.Concurrent;
using System.Threading;



ItemGeneratorConfig config = AppConfigProvider.LoadConfiguration<ItemGeneratorConfig>();

CancellationTokenSource cancellationTokenSource = new();

//Instantiate mqttManager
MqttManager mqttManager = await MqttManagerFactory.CreateDefault(cancellationTokenSource);

//Instantiate bridge (AIO MQTT Broker)
MqttManager iotmqBridge = await MqttManagerFactory.CreateIotmqBridge(cancellationTokenSource);

//Prepare Gracefull Exit
AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
{
    await mqttManager.Shutdown(config.FrequencyMilliseconds);
    Console.WriteLine($"[{DateTime.UtcNow}]\tMqtt.ItemGenerator finished.");
};

Console.CancelKeyPress += async (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    await mqttManager.Shutdown(config.FrequencyMilliseconds);
    Environment.Exit(0);
};

var host = new HostBuilder()
            .ConfigureServices((hostContext, services) =>
            {

                // Add logging if needed
                services.AddLogging();
                services.AddSingleton(config);
                services.AddSingleton(mqttManager);
                services.AddSingleton(iotmqBridge); 

                // Add background tasks
                services.AddHostedService<ItemGeneratorService>();
                services.AddHostedService<ItemTerminatorService>();
                services.AddHostedService<ItemStatsService>();
            })
            .Build();

await host.RunAsync(cancellationTokenSource.Token);
await host.WaitForShutdownAsync(cancellationTokenSource.Token);






