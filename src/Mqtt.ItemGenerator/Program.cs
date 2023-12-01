using Mqtt.ItemGenerator;
using Mqtt.Shared;
using System.Collections.Concurrent;
using System.Threading;



ItemGeneratorConfig config = AppConfigProvider.LoadConfiguration<ItemGeneratorConfig>();

CancellationTokenSource cancellationTokenSource = new();

//Instantiate mqttManager
MqttManager mqttManager = await MqttManagerFactory.Create(cancellationTokenSource);


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


if (config.EnableTermination)
{
    Console.WriteLine($"[{DateTime.UtcNow}]\tItem Termination Zone: '{config.TerminationZone}'.");
    ItemTerminator terminator = new(config, mqttManager);
    await terminator.StartTerminatingItems();
}

if (config.EnableGeneration)
{
    if (config.SimulationStartDelayMilliseconds > 0)
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tItem Generation start delayed by {config.SimulationStartDelayMilliseconds} milliseconds...");
        await Task.Delay(config.SimulationStartDelayMilliseconds);
    }

    //Start Generating Clases
    ItemGenerator producer = new(config, mqttManager, cancellationTokenSource.Token);
    await producer.StartGeneratingItems();
}


Console.ReadLine();






