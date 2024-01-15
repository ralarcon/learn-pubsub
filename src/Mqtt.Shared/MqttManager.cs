using Mqtt.ItemGenerator;
using Mqtt.Shared;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime;
using System.Text;
using System.Text.Json;

public class MqttManager
{
    private readonly IManagedMqttClient _mqttClient;
    private readonly MqttConfig _config;
    private readonly CancellationToken _cancellationToken;
    private readonly Stopwatch _aliveWatch = Stopwatch.StartNew();

    //Handler for each topic subscribed (payload, sourceTs, receiveTs, hopLatency)
    private ConcurrentDictionary<string, Func<ArraySegment<byte>, DateTime, DateTime, double, Task>> _topicHandlers= new();

    public MqttManager(MqttConfig config, CancellationToken cancellationToken)
    {
        _config = config;
        _mqttClient = new MqttFactory().CreateManagedMqttClient();
        _cancellationToken = cancellationToken;
    }

    public bool IsConnected => _mqttClient.IsConnected;
    public bool IsStarted => _mqttClient.IsStarted;
    public int PendingAppMessages => _mqttClient.PendingApplicationMessagesCount;

    public async Task StartMqttClientAsync()
    {
        var options = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(new MqttClientOptionsBuilder()
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                .WithClientId(_config.ClientId)
                .WithTcpServer(_config.MqttServer, _config.MqttPort)
                .WithWillQualityOfServiceLevel((MqttQualityOfServiceLevel)_config.QoS)
                .WithWillTopic($"clients/{_config.ClientId}/status")
                .WithWillMessageExpiryInterval(60)
                .WithWillRetain(true)
                .WithWillPayload(Encoding.UTF8.GetBytes($"Client {_config.ClientId} OFFLINE"))
                .WithCredentials(_config.Username, _config.Password)
                .Build())
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += ApplicationMessageReceivedAsync;
        _mqttClient.ConnectedAsync += ClientConnectedAsync;
        _mqttClient.DisconnectedAsync += ClientDisconnectedAsync;
        _mqttClient.ConnectingFailedAsync += ConnectingFailedAsync;
        _mqttClient.SynchronizingSubscriptionsFailedAsync += _mqttClient_SynchronizingSubscriptionsFailedAsync;
        _mqttClient.ApplicationMessageSkippedAsync += _mqttClient_ApplicationMessageSkippedAsync;
        await _mqttClient.StartAsync(options).ConfigureAwait(false);
    }

    public async Task StopMqttClientAsync()
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} stopping.");
        foreach(var topicSubscribed in _topicHandlers.Keys)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tUnsubscribing from topic '{topicSubscribed}'");
            await _mqttClient.UnsubscribeAsync(topicSubscribed).ConfigureAwait(false);
        }
        await _mqttClient.StopAsync();
        Console.WriteLine($"[{DateTime.UtcNow}]\tMQTT client stopped.");
    }
    public async Task PublishMessageAsync(string message, string topic)
    {
        var payload = Encoding.UTF8.GetBytes(message);
        await PublishMessageAsync(payload, topic);
    }
    public async Task PublishMessageAsync(byte[] payload, string topic)
    {
        var appMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)_config.QoS)
            .WithUserProperty($"SourceTs", JsonSerializer.Serialize(DateTime.UtcNow))
            .Build();

        await _mqttClient.EnqueueAsync(appMessage).ConfigureAwait(false);
    }

    public async Task PublishHopLatency(ItemTransitionLatency hop)
    {
        var appMessage = new MqttApplicationMessageBuilder()
            .WithTopic(TopicsDefinition.ItemsLatencies()) 
            .WithPayload(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(hop)))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.EnqueueAsync(appMessage).ConfigureAwait(false);
    }
    public async Task PublishStatusAsync(byte[] payload, string topic)
    {
        if (_config.TrackStatus)
        {
            var appMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                //.WithQualityOfServiceLevel((MqttQualityOfServiceLevel)_config.QoS)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .WithRetainFlag(true)
                .Build();

            await _mqttClient.EnqueueAsync(appMessage).ConfigureAwait(false);
        }
    }

    public async Task RemoveStatusAsync(string topic)
    {
        var appMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(new byte[] { })
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();

        await _mqttClient.EnqueueAsync(appMessage).ConfigureAwait(false);
    }

    public async Task SubscribeTopicAsync(string topic, Func<ArraySegment<byte>,  DateTime, DateTime, double, Task> topicHandler)
    {
        if (topicHandler is null) throw new ArgumentNullException(nameof(topicHandler));
        if (string.IsNullOrEmpty(topic)) throw new ArgumentException(topic);

        await _mqttClient.SubscribeAsync(topic, (MqttQualityOfServiceLevel)_config.QoS).ConfigureAwait(false);

        if (_topicHandlers.TryAdd(topic, topicHandler))
        {

            Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} subscribed to {topic} topic.");
        }
        else
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} already subscribed to {topic} topic.");
            throw new Exception($"Client {_config.ClientId} already subscribed to {topic} topic.");
        }
    }
    public async Task UnsubscribeTopicAsync(string topic)
    {
        await _mqttClient.UnsubscribeAsync(topic).ConfigureAwait(false);
    }

    public async Task Shutdown(int delayMilliseconds = 500)
    {
        if (!_cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tMqttManager shutdown requested...");
            await Task.Delay(delayMilliseconds).ConfigureAwait(false);
            await StopMqttClientAsync().ConfigureAwait(false);
        }
    }

    private async Task ClientConnectedAsync(MqttClientConnectedEventArgs eventArgs)
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} connected to mqtt://{_config.MqttServer}:{_config.MqttPort}.");

        var appMessage = new MqttApplicationMessageBuilder()
            .WithTopic($"clients/{_config.ClientId}/status")
            .WithPayload(Encoding.UTF8.GetBytes($"ONLINE"))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();
        await _mqttClient.EnqueueAsync(appMessage).ConfigureAwait(false);
    }


    private async Task ClientDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs)
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} disconnected from mqtt://{_config.MqttServer}:{_config.MqttPort}. {eventArgs.ReasonString}");
        await Task.CompletedTask;
    }
    private async Task ConnectingFailedAsync(ConnectingFailedEventArgs eventArgs)
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} FAILED to connected to mqtt://{_config.MqttServer}:{_config.MqttPort}. Exception:\n{eventArgs.Exception}");
        await Task.CompletedTask;
    }

    private async Task ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        var payload = eventArgs.ApplicationMessage.PayloadSegment;

        async Task InvokeMessageHandler()
        {
            if (payload.Array != null && payload.Count > 0)
            {
                _ = Task.Factory.StartNew( async () =>
                {
                    DateTime receiveTs = DateTime.UtcNow;
                    DateTime sourceTs = DateTime.MinValue;
                    double hopLatency = 0;
                    if (eventArgs.ApplicationMessage.UserProperties != null && eventArgs.ApplicationMessage.UserProperties.Count > 0)
                    {
                        var sourceTsJson = eventArgs.ApplicationMessage.UserProperties.Where(p => p.Name == "SourceTs").FirstOrDefault();
                        if (sourceTsJson != null)
                        {
                            sourceTs = JsonSerializer.Deserialize<DateTime>(sourceTsJson.Value);
                            hopLatency = (receiveTs - sourceTs).TotalMilliseconds;
                        }                   
                    }
                    await _topicHandlers[eventArgs.ApplicationMessage.Topic](payload, sourceTs, receiveTs, hopLatency).ConfigureAwait(false);
                }, _cancellationToken, TaskCreationOptions.PreferFairness, TaskScheduler.Default).ConfigureAwait(false);
            }
            else
            {
                Console.Error.WriteLine($"[{DateTime.UtcNow}]\tPayload empty received from topic {eventArgs.ApplicationMessage.Topic}");
            }
        }

        _ = Task.Run(InvokeMessageHandler, _cancellationToken).ConfigureAwait(false);
    }

    private Task _mqttClient_SynchronizingSubscriptionsFailedAsync(ManagedProcessFailedEventArgs arg)
    {
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} FAILED to synchronize subscriptions. Exception:\n{arg.Exception}");
        Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} retrying to synchronize subscriptions...");

        arg.RemovedSubscriptions.ForEach(async topic =>
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} unsubscribing from topic '{topic}'");
            await _mqttClient.UnsubscribeAsync(topic).ConfigureAwait(false);
        });

        arg.AddedSubscriptions.ForEach(async topic =>
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tClient {_config.ClientId} subscribing to topic '{topic}'");
            await _mqttClient.SubscribeAsync(topic).ConfigureAwait(false);
        });

        return Task.CompletedTask;
    }

    private async Task _mqttClient_ApplicationMessageSkippedAsync(ApplicationMessageSkippedEventArgs arg)
    {
        if (arg.ApplicationMessage.ApplicationMessage.PayloadSegment.Array != null)
        {
            await PublishStatusAsync(arg.ApplicationMessage.ApplicationMessage.PayloadSegment.Array, $"clients/{_config.ClientId}/skipped/{arg.ApplicationMessage.ApplicationMessage.Topic}");
        }
    }

}
