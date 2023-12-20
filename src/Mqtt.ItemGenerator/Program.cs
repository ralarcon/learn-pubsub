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

await host.RunAsync();


//if (config.EnableTermination)
//{
//    Console.WriteLine($"[{DateTime.UtcNow}]\tItem Termination Zone: '{config.ItemsTermination}'.");
//    ItemTerminator terminator = new(config, iotmqBridge);
//    await terminator.StartTerminatingItemsAsync().ConfigureAwait(false);
//}

//if (config.EnableGeneration)
//{
//    if (config.SimulationStartDelayMilliseconds > 0)
//    {
//        Console.WriteLine($"[{DateTime.UtcNow}]\tItem Generation start delayed by {config.SimulationStartDelayMilliseconds} milliseconds...");
//        await Task.Delay(config.SimulationStartDelayMilliseconds);
//    }

//    //Start Generating Clases
//    ItemGenerator producer = new(config, mqttManager, cancellationTokenSource.Token);
//    await producer.StartGeneratingItems().ConfigureAwait(false);
//}


Console.ReadLine();






