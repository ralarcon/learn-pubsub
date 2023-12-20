using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Mqtt.Shared;
using Mqtt.ZoneSimulator;
using System.Security.Cryptography;
using System.Diagnostics;

ZoneSimulatorConfig config = AppConfigProvider.LoadConfiguration<ZoneSimulatorConfig>();

CancellationTokenSource cancellationTokenSource = new();

//Instantiate mqttManager
MqttManager mqttManager = await MqttManagerFactory.CreateDefault(cancellationTokenSource.Token);

//Prepare Gracefull Exit
AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
{
    await mqttManager.Shutdown();
    Console.WriteLine($"[{DateTime.UtcNow}]\tMqtt.ItemGenerator finished.");
};

Console.CancelKeyPress += async (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    await mqttManager.Shutdown();
    Environment.Exit(0);
};



Console.WriteLine($"[{DateTime.UtcNow}]\tSimulation for '{config.Zone}'. Source Zone: '{TopicsDefinition.Items(config.ItemsSource)}'; Destination Zone: '{config.ItemsDestination}'.");


//TODO: Model beter the converyor + positions "system" to better manager ids, instances & naming

//Prepare Zone Conveyors
ConveyorSystem zoneConveyors = new ConveyorSystem(config, mqttManager, config.ItemsSource, config.ItemsDestination);

await zoneConveyors.PrepareConveyors();
await zoneConveyors.StartSimulationAsync();

Console.WriteLine($"[{DateTime.UtcNow}]\tSimulation running... ");
Stopwatch reportAliveWatch = Stopwatch.StartNew();
while (true)
{
    if(reportAliveWatch.ElapsedMilliseconds > 30000)
    {
        reportAliveWatch.Restart();
        Console.WriteLine();
        Console.WriteLine($"[{DateTime.UtcNow}]\tSimulation alive... ");
        Console.WriteLine($"[{DateTime.UtcNow}]\tItems transited in conveyor {zoneConveyors.Conveyors.LastOrDefault()?.Id}: {zoneConveyors.Conveyors.LastOrDefault()?.ItemsOut}");
        Console.WriteLine($"[{DateTime.UtcNow}]\tMqtt Status -> Connected: {mqttManager.IsConnected}; Started: {mqttManager.IsStarted}; Pending Messages: {mqttManager.PendingAppMessages}");
    }
    await Task.Delay(1000);
};






