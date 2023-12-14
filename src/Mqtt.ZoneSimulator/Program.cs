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

ZoneSimulatorConfig config = AppConfigProvider.LoadConfiguration<ZoneSimulatorConfig>();

CancellationTokenSource cancellationTokenSource = new();

//Instantiate mqttManager
MqttManager mqttManager = await MqttManagerFactory.CreateDefault(cancellationTokenSource);

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
ConveyorSystem conveyors = new ConveyorSystem(config, mqttManager, config.ItemsSource, config.ItemsDestination);

await conveyors.PrepareConveyors();
await conveyors.StartSimulationAsync();

Console.WriteLine($"[{DateTime.UtcNow}]\tSimulation started.");
while (true)
{
    await Task.Delay(1000);
};






