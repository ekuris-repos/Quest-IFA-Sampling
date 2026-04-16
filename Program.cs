using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestIFASampling.TelemetryConfig;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(TelemetryServiceConfiguration.ConfigureServices)
    .Build();

host.Run();
