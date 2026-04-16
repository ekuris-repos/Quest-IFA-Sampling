using System.Diagnostics;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestIFASampling.TelemetryConfig;
using Xunit;

namespace QuestIFASampling.Tests.TelemetryConfig;

public class TelemetryServiceConfigurationTests
{
    private const string WorkerActivitySourceName = "Microsoft.Azure.Functions.Worker.Test";
    private const string TestConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";

    [Fact]
    public async Task BaselineFunctionsApplicationInsights_EmitsInvocationDependencyTelemetry()
    {
        var services = CreateServiceCollection();

        services.Configure<ApplicationInsightsServiceOptions>(options =>
        {
            options.ConnectionString = TestConnectionString;
            options.EnableAdaptiveSampling = false;
        });

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        await using var provider = services.BuildServiceProvider();
        var module = provider
            .GetServices<ITelemetryModule>()
            .Single(candidate => candidate.GetType().FullName == TelemetryServiceConfiguration.FunctionsTelemetryModuleTypeName);
        var telemetryConfiguration = CreateTelemetryConfiguration(provider.GetRequiredService<CollectingTelemetryChannel>());

        module.Initialize(telemetryConfiguration);

        using var activitySource = new ActivitySource(WorkerActivitySourceName);
        Activity? activity;

        using (activity = activitySource.StartActivity("function SampleHttpTrigger", ActivityKind.Internal))
        {
            Assert.NotNull(activity);
        }

        var dependency = Assert.Single(provider.GetRequiredService<CollectingTelemetryChannel>().Items.OfType<DependencyTelemetry>());
        Assert.Equal("InProc", dependency.Type);
        Assert.Equal(activity?.TraceId.ToString(), dependency.Context.Operation.Id);
    }

    [Fact]
    public async Task SampleConfiguration_RemovesInvocationTelemetryModule_ButKeepsDirectWorkerLoggingEnabled()
    {
        var services = CreateServiceCollection();

        TelemetryServiceConfiguration.ConfigureServices(CreateHostBuilderContext(), services);

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(ITelemetryModule) &&
            descriptor.ImplementationType?.FullName == TelemetryServiceConfiguration.FunctionsTelemetryModuleTypeName);

        await using var provider = services.BuildServiceProvider();
        var workerOptions = provider.GetRequiredService<IOptions<WorkerOptions>>().Value;
        var aiOptions = provider.GetRequiredService<IOptions<ApplicationInsightsServiceOptions>>().Value;

        Assert.True(workerOptions.Capabilities.TryGetValue(TelemetryServiceConfiguration.WorkerApplicationInsightsLoggingEnabledCapability, out var capabilityValue));
        Assert.Equal(bool.TrueString, capabilityValue);
        Assert.False(aiOptions.EnableAdaptiveSampling);
    }

    [Fact]
    public async Task SampleConfiguration_DoesNotHaveAModuleThatCanCreateInvocationDependencyTelemetry()
    {
        var services = CreateServiceCollection();

        TelemetryServiceConfiguration.ConfigureServices(CreateHostBuilderContext(), services);

        await using var provider = services.BuildServiceProvider();
        var telemetryModules = provider.GetServices<ITelemetryModule>().ToList();

        Assert.DoesNotContain(telemetryModules, module => module.GetType().FullName == TelemetryServiceConfiguration.FunctionsTelemetryModuleTypeName);
    }

    private static ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITelemetryChannel, CollectingTelemetryChannel>();
        services.AddSingleton<CollectingTelemetryChannel>(provider => (CollectingTelemetryChannel)provider.GetRequiredService<ITelemetryChannel>());
        return services;
    }

    private static HostBuilderContext CreateHostBuilderContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sampling:Enabled"] = "true",
                ["Sampling:Percentage"] = "100",
                ["Sampling:ExcludedTypes"] = "Request;Exception",
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = TestConnectionString
            })
            .Build();

        return new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = configuration
        };
    }

    private static TelemetryConfiguration CreateTelemetryConfiguration(CollectingTelemetryChannel channel)
    {
        return new TelemetryConfiguration
        {
            ConnectionString = TestConnectionString,
            TelemetryChannel = channel
        };
    }

    private sealed class CollectingTelemetryChannel : ITelemetryChannel
    {
        public List<ITelemetry> Items { get; } = new();

        public void Send(ITelemetry item)
        {
            Items.Add(item);
        }

        public void Flush()
        {
        }

        public bool? DeveloperMode { get; set; }

        public string? EndpointAddress { get; set; }

        public void Dispose()
        {
        }
    }
}