using Microsoft.Extensions.Configuration;
using System;

internal class AppConfig
{
    public MqttConfig MqttConfig { get; set; } = new MqttConfig();
    public int ProcessingDelayInMilliseconds { get; set; } = 0;
    public int PublishingIntervalInMilliseconds { get; set; } = 1000;
    public bool Publisher { get; set; } = false;
    public bool Subscriber { get; set; } = false;
    public bool ShowConsoleEchoes { get; set; } = false;
    public bool AutoConfigurePubSub { get; set; } = false;
}

internal class AppConfigProvider
{
    public static AppConfig LoadConfiguration()
    {

        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Environment.CurrentDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(Environment.GetCommandLineArgs())
            .AddUserSecrets<Program>()
            .Build();

        var appConfig = new AppConfig();
        config.GetSection("AppConfig").Bind(appConfig);

        if (appConfig.AutoConfigurePubSub)
        {
            Console.WriteLine($"[{DateTime.UtcNow}]\tThe client is configure to setup automatically the 'pubsub' loopback (AutoConfigurePubSub=true)'.");
            Console.WriteLine($"[{DateTime.UtcNow}]\tIgnoring ClientId & Mqtt Topics from configuration (if any).");
            string clientId = Environment.MachineName;
            appConfig.MqttConfig.ClientId = clientId;
            appConfig.MqttConfig.EchoTopic = $"pubsub-tests/replicas/echoes/{clientId}";
            appConfig.MqttConfig.PublishTopic = $"pubsub-tests/replicas/{clientId}/out";
            appConfig.MqttConfig.SubscribeTopic = $"pubsub-tests/replicas/{clientId}/out";
        }

        return appConfig;
    }
}

