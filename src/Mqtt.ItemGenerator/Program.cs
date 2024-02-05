using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mqtt.ItemGenerator;
using Mqtt.Shared;
using System.Collections.Concurrent;
using System.Threading;


ItemGeneratorConfig config = AppConfigProvider.LoadConfiguration<ItemGeneratorConfig>();

CancellationTokenSource cancellationTokenSource = new();

var host = new HostBuilder()
    .ConfigureServices((hostContext, services) =>
    {

        // Add logging if needed
        services.AddLogging();
        services.AddSingleton(config);

        // Add background tasks
        services.AddHostedService<ItemGeneratorService>();
        services.AddHostedService<ItemTerminatorService>();
        services.AddHostedService<ItemStatsService>();
    })
    .Build();

await host.StartAsync(cancellationTokenSource.Token);
await host.WaitForShutdownAsync(cancellationTokenSource.Token);






