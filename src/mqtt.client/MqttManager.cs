using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;

internal class MqttManager
{
    private readonly IManagedMqttClient _mqttClient;
    private readonly AppConfig _config;
    private readonly CancellationToken _cancellationToken;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Stopwatch _aliveWatch = Stopwatch.StartNew();
    public MqttManager(AppConfig config, CancellationToken cancellationToken)
    {
        _config = config;
        _mqttClient = new MqttFactory().CreateManagedMqttClient();
        _cancellationToken = cancellationToken;
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.General);
    }

    public async Task StartMqttClient()
    {
        var options = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(new MqttClientOptionsBuilder()
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                .WithClientId(_config.MqttConfig.ClientId)
                .WithTcpServer(_config.MqttConfig.MqttServer, _config.MqttConfig.MqttPort)
                .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .WithWillTopic($"clients/{_config.MqttConfig.ClientId}/status")
                .WithWillMessageExpiryInterval(60)
                .WithWillRetain(true)
                .WithWillPayload(Encoding.UTF8.GetBytes($"Client {_config.MqttConfig.ClientId} OFFLINE"))
                .WithCredentials("", "")
                .Build())
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedAsync;
        _mqttClient.ConnectedAsync += ClientConnectedAsync;
        _mqttClient.DisconnectedAsync += ClientDisconnectedAsync;
        _mqttClient.ConnectingFailedAsync += ConnectingFailedAsync;
        await _mqttClient.StartAsync(options);
    }

    public async Task StopMqttClient()
    {
        await _mqttClient.UnsubscribeAsync(_config.MqttConfig.SubscribeTopic);
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.MqttConfig.ClientId} UNsubscribed from {_config.MqttConfig.SubscribeTopic}.");
        await _mqttClient.StopAsync();
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
    private async Task ConnectingFailedAsync(ConnectingFailedEventArgs eventArgs)
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.MqttConfig.ClientId} FAILED to connected to mqtt://{_config.MqttConfig.MqttServer}:{_config.MqttConfig.MqttPort}. Exception:\n{eventArgs.Exception}");
        await Task.CompletedTask;
    }

    private async Task ClientConnectedAsync(MqttClientConnectedEventArgs eventArgs)
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.MqttConfig.ClientId} connected to mqtt://{_config.MqttConfig.MqttServer}:{_config.MqttConfig.MqttPort}.");

        var appMessage = new MqttApplicationMessageBuilder()
            .WithTopic($"clients/{_config.MqttConfig.ClientId}/status")
            .WithPayload(Encoding.UTF8.GetBytes($"Client {_config.MqttConfig.ClientId} ONLINE"))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .WithRetainFlag(true)
            .Build();
        await _mqttClient.EnqueueAsync(appMessage);

        if (_config.Subscriber)
        {
            await _mqttClient.SubscribeAsync(_config.MqttConfig.SubscribeTopic);
            Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.MqttConfig.ClientId} subscribed to {_config.MqttConfig.SubscribeTopic}.");
        }
        else
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tSkipping subscription to configured topic (if any). Client not configured to subscribe to topic.");
        }
    }


    private async Task ClientDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.MqttConfig.ClientId} disconnected from mqtt://{_config.MqttConfig.MqttServer}:{_config.MqttConfig.MqttPort}: {eventArgs.ReasonString}");
        await Task.CompletedTask;
    }

    private async Task ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        DateTime receiveTime = DateTime.UtcNow;

        async Task ProcessMessageAsync()
        {
            var payload = Encoding.UTF8.GetString(eventArgs.ApplicationMessage.PayloadSegment);

            if (!string.IsNullOrEmpty(payload))
            {
                Message message = JsonSerializer.Deserialize<Message>(payload, _jsonOptions)!;
                //Simulate Processing Time
                await Task.Delay(_config.ProcessingDelayInMilliseconds);

                DateTime echoTime = DateTime.UtcNow;

                // Echo the message to another topic
                EchoMessage echo = new()
                {
                    ClientId = _config.MqttConfig.ClientId,
                    OriginalMessage = message,
                    DestinationTopic = _config.MqttConfig.EchoTopic,
                    ReceiveTimestamp = receiveTime,
                    EchoTimestamp = echoTime,
                    SourceTopic = eventArgs.ApplicationMessage.Topic,
                    SourceToEchoDiff = echoTime - message.SourceTimestamp,
                    SourceToEchoMillesconds = (echoTime - message.SourceTimestamp).TotalMilliseconds,
                    SequenceId = message.SequenceId,
                    BatchId = message.BatchId,
                    IsProcessingSimulated = _config.ProcessingDelayInMilliseconds != 0
                };

                _ = Task.Run(async () =>
                {
                    await PublishEchoMessageAsync(echo);
                }, _cancellationToken);
            }
            else
            {
                Console.Error.WriteLine("Payload is empty");
            }
        }
        _ = Task.Run(ProcessMessageAsync, _cancellationToken);
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

        if (_aliveWatch.Elapsed.TotalSeconds > 60 && !_config.ShowConsoleEchoes)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tEchoing messages to configured Echo topic: {_config.MqttConfig.EchoTopic} ");
            _aliveWatch.Restart();
        }
        else if (_config.ShowConsoleEchoes)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tEchoing message: {JsonSerializer.Serialize(echoMessage)}");
            Console.WriteLine();
        }
    }
}
