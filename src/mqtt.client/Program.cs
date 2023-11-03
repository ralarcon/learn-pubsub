using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

// See https://aka.ms/new-console-template for more information

var appConfig = AppConfigProvider.LoadConfiguration();

Console.WriteLine($"[{DateTime.UtcNow}]\tMQTT Echoer: '{appConfig.MqttConfig.ClientId}'");
CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
var mqttManager = new MqttManager(appConfig, cancellationTokenSource.Token);

if (appConfig.Subscriber)
{
    Console.WriteLine($"[{DateTime.UtcNow}]\tSubscribing to '{appConfig.MqttConfig.SubscribeTopic}' at 'mqtt://{appConfig.MqttConfig.MqttServer}:{appConfig.MqttConfig.MqttPort}'");
}

if (appConfig.Publisher)
{
    Console.WriteLine($"[{DateTime.UtcNow}]\tPublishing to '{appConfig.MqttConfig.PublishTopic}'. Message Interval: {appConfig.PublishingIntervalInMilliseconds} msec. Processing delay: {appConfig.ProcessingDelayInMilliseconds} msec.");
}

//Prepare application shutdown
AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
{
    Console.WriteLine($"[{DateTime.UtcNow}]\tExiting...");
    await mqttManager.StopMqttClient();
    Console.WriteLine($"[{DateTime.UtcNow}]\tMQTT client stopped.");
    cancellationTokenSource.Cancel();
};


// Start the MQTT client
await mqttManager.StartMqttClient();

Stopwatch aliveWatch = Stopwatch.StartNew();
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
        if (aliveWatch.Elapsed.TotalSeconds >= 60)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tPublishing messages to configured topic: {appConfig.MqttConfig.PublishTopic}.");
            aliveWatch.Restart();
        }
    }
    await Task.Delay(TimeSpan.FromSeconds(1));
    if (cancellationTokenSource.IsCancellationRequested)
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tCancellation requested. Finishing EchoClient.");
    }
}