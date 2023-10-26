using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

// See https://aka.ms/new-console-template for more information

var appConfig = AppConfigProvider.LoadConfiguration();

Console.WriteLine($"MQTT Echoer: '{appConfig.MqttConfig.ClientId}'");

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

var mqttManager = new MqttManager(appConfig, cancellationTokenSource.Token);

AppDomain.CurrentDomain.ProcessExit += (s, e) =>
{
    Console.WriteLine("Exiting...");
    cancellationTokenSource.Cancel();
};

if (appConfig.Suscriber)
{
    Console.WriteLine($"Subscribing to '{appConfig.MqttConfig.SubscribeTopic}' at 'mqtt://{appConfig.MqttConfig.MqttServer}:{appConfig.MqttConfig.MqttPort}'");
}

if (appConfig.Publisher)
{
    Console.WriteLine($"Publishing to '{appConfig.MqttConfig.PublishTopic}'. Message Interval: {appConfig.PublishingIntervalInMilliseconds} msec. Processing delay: {appConfig.ProcessingDelayInMilliseconds} msec.");
}

// Start the MQTT client
await mqttManager.StartMqttClient();

Stopwatch uptimeWatch = Stopwatch.StartNew();
Guid batchId = Guid.NewGuid();
int sequence = 0;
while (true && !cancellationTokenSource.IsCancellationRequested)
{
    if (appConfig.Publisher)
    {
        var message = new Message
        {
            ClientId = appConfig.MqttConfig.ClientId,
            SourceTimestamp = DateTime.UtcNow,
            Content = $"Message {sequence} from batch {batchId.ToString()}",
            SequenceId = sequence,
            BatchId = batchId
        };
        sequence++;
        await mqttManager.PublishMessageAsync(message);
        if (uptimeWatch.Elapsed.TotalSeconds % 60 == 0)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tUptime: {uptimeWatch.Elapsed.TotalSeconds} seconds. Publishing messages to configured topic.");
        }
    }
    await Task.Delay(TimeSpan.FromSeconds(1));
}