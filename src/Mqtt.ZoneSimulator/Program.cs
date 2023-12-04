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
using Mqtt.ConveyorSimulator;

ConveyorSimulatorConfig config = AppConfigProvider.LoadConfiguration<ConveyorSimulatorConfig>();

CancellationTokenSource cancellationTokenSource = new();

//Instantiate mqttManager
MqttManager mqttManager = await MqttManagerFactory.Create(cancellationTokenSource);

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



Console.WriteLine($"[{DateTime.UtcNow}]\tSimulation for '{config.Zone}'. Source Zone: '{TopicsDefinition.Items(config.SourceZone)}'; Destination Zone: '{config.DestinationZone}'.");



//TODO: Model beter the converyor + junctions "system" to better manager ids, instances & naming

//Prepare Zone Conveyors
ConveyorSystem conveyors = new ConveyorSystem(config, mqttManager);

//Prepare Inter-Zone Junctions
JunctionsSet intertZoneJunctions = new(config, mqttManager);
Junction previousZoneConnection = intertZoneJunctions.CreateJunction(TopicsDefinition.Items(config.SourceZone), TopicsDefinition.ConveyorSensor(config.Zone, conveyors.Conveyors.First().Id, nameof(ConveyorSensor.In)), "wh", config.SourceZone);
Junction nextZoneConnection = intertZoneJunctions.CreateJunction(TopicsDefinition.ConveyorSensor(config.Zone, conveyors.Conveyors.Last().Id, nameof(ConveyorSensor.Out)), TopicsDefinition.Items(config.DestinationZone), config.SourceZone, config.DestinationZone);
await intertZoneJunctions.StartSimulationAsync();

await conveyors.StartSimulationAsync();

Console.ReadLine();






