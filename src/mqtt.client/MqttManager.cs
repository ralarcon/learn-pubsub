using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using System;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;

internal class MqttManager
{
    private readonly IManagedMqttClient _mqttClient;
    private readonly AppConfig _config;

    public MqttManager(AppConfig config)
    {
        _config = config;
        _mqttClient = new MqttFactory().CreateManagedMqttClient();
    }

    public async Task StartMqttClient()
    {
        var options = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(new MqttClientOptionsBuilder()
                .WithClientId(_config.MqttConfig.ClientId)
                .WithTcpServer(_config.MqttConfig.MqttServer, _config.MqttConfig.MqttPort)
                .WithCredentials("", "")
                .Build())
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedAsync;
        _mqttClient.ConnectedAsync += ClientConnectedAsync;
        _mqttClient.DisconnectedAsync += ClientDisconnectedAsync;
        _mqttClient.ConnectingFailedAsync += ConnectingFailedAsync;
        await _mqttClient.StartAsync(options);
    }

    private async Task ConnectingFailedAsync(ConnectingFailedEventArgs eventArgs)
    {
        Console.WriteLine($"Client {_config.MqttConfig.ClientId} FAILED to connected to mqtt://{_config.MqttConfig.MqttServer}:{_config.MqttConfig.MqttPort}. Exception:\n{eventArgs.Exception}");
        await Task.CompletedTask;
    }

    private async Task ClientConnectedAsync(MqttClientConnectedEventArgs eventArgs)
    {
        Console.WriteLine($"Client {_config.MqttConfig.ClientId} connected to mqtt://{_config.MqttConfig.MqttServer}:{_config.MqttConfig.MqttPort}.");
        await _mqttClient.SubscribeAsync(_config.MqttConfig.SubscribeTopic);
    }

    private async Task ClientDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
    {
        Console.WriteLine($"Client {_config.MqttConfig.ClientId} disconnected from mqtt://{_config.MqttConfig.MqttServer}:{_config.MqttConfig.MqttPort}: {eventArgs.ReasonString}");
        await Task.CompletedTask;
    }

    private async Task ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        DateTime receiveTime = DateTime.UtcNow;

        var payload = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.PayloadSegment);

        if (!string.IsNullOrEmpty(payload))
        {
            Message message = JsonSerializer.Deserialize<Message>(payload)!;
            //Simulate Processing Time
            await Task.Delay(_config.ProcessingDelayInMilliseconds);

            DateTime echoTime = DateTime.UtcNow;

            // Echo the message to another topic
            EchoMessage echo = new EchoMessage
            {
                ClientId = _config.MqttConfig.ClientId,
                OriginalMessage = message,
                DestinationTopic = _config.MqttConfig.EchoTopic,
                ReceiveTimestamp = receiveTime,
                EchoTimestamp = echoTime,
                SourceTopic = eventArgs.ApplicationMessage.Topic,
                SourceToEchoDiff = echoTime - message.SourceTimestamp,
                Id = message.Id
            };

            Console.WriteLine($"{DateTime.UtcNow}\tEchoing message: {JsonSerializer.Serialize(echo)}");
            Console.WriteLine();
            await PublishEchoMessageAsync(echo);
        }
        else
        {
            Console.Error.WriteLine("Payload is empty");
        }
    }

    private async Task PublishEchoMessageAsync(EchoMessage echoMessage)
    {
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(echoMessage));

        var appMessage = new MqttApplicationMessageBuilder()
            .WithTopic(_config.MqttConfig.EchoTopic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.EnqueueAsync(appMessage);
    }

    public async Task PublishMessageAsync(Message message)
    {
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var appMessage = new MqttApplicationMessageBuilder()
            .WithTopic(_config.MqttConfig.PublishTopic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.EnqueueAsync(appMessage);
    }
}
