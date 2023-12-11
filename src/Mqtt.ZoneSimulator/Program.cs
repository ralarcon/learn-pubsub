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



Console.WriteLine($"[{DateTime.UtcNow}]\tSimulation for '{config.Zone}'. Source Zone: '{TopicsDefinition.Items(config.SourceZone)}'; Destination Zone: '{config.DestinationZone}'.");



//TODO: Model beter the converyor + positions "system" to better manager ids, instances & naming

//Prepare Zone Conveyors
ConveyorSystem conveyors = new ConveyorSystem(config, mqttManager, config.SourceZone, config.DestinationZone);

//Prepare Inter-Zone Positions
//PositionSet intertZonePositions = new(config, mqttManager);
//Position previousZoneConnection = intertZonePositions.CreatePosition(TopicsDefinition.Items(config.SourceZone), TopicsDefinition.ConveyorSensor(config.Zone, conveyors.Conveyors.First().Id, nameof(ConveyorSensor.In)), config.SourceZone, $"{conveyors.Conveyors.First().Id}");
//Position nextZoneConnection = intertZonePositions.CreatePosition(TopicsDefinition.ConveyorSensor(config.Zone, conveyors.Conveyors.Last().Id, nameof(ConveyorSensor.Out)), TopicsDefinition.Items(config.DestinationZone), $"{conveyors.Conveyors.Last().Id}", config.DestinationZone);
//await intertZonePositions.StartSimulationAsync();

await conveyors.PrepareConveyors();
await conveyors.StartSimulationAsync();

Console.ReadLine();






