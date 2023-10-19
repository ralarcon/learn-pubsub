using Microsoft.Extensions.Configuration;

// See https://aka.ms/new-console-template for more information

var appConfig = AppConfigProvider.LoadConfiguration();

Console.WriteLine($"MQTT Echoer: '{appConfig.MqttConfig.ClientId}'");

var mqttManager = new MqttManager(appConfig);

Console.WriteLine($"Subscribing to '{appConfig.MqttConfig.SubscribeTopic}' at 'mqtt://{appConfig.MqttConfig.MqttServer}:{appConfig.MqttConfig.MqttPort}'");
Console.WriteLine($"Publishing to '{appConfig.MqttConfig.PublishTopic}' with a delay of {appConfig.ProcessingDelayInMilliseconds} milliseconds");

// Start the MQTT client
await mqttManager.StartMqttClient();

int msgId = 0;
while (true)
{
    if (appConfig.Publisher)
    {
        var message = new Message
        {
            ClientId = appConfig.MqttConfig.ClientId,
            SourceTimestamp = DateTime.UtcNow,
            Content = $"Message {msgId}",
            Id = msgId
        };
        msgId++;
        await mqttManager.PublishMessageAsync(message);
        if (msgId % 15 == 0)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tPublishing messages to configured topic.");
        }
    }
    else
    {
        msgId++;
        if (msgId % 15 == 0)        
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tSkipping publishing to configured topic (if any). Client not configured to publish messages.");
        }
    }
    await Task.Delay(TimeSpan.FromSeconds(1));
}