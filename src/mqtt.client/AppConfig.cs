using Microsoft.Extensions.Configuration;
using System;

internal class AppConfig
{
    public MqttConfig MqttConfig { get; set; } = new MqttConfig();
    public int ProcessingDelayInMilliseconds { get; set; } = 0;
    public bool Publisher { get; set; } = true;
    public bool Suscriber { get; set; } = true;
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

        return appConfig;
    }
}

