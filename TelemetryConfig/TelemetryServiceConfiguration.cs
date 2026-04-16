using System.Linq;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace QuestIFASampling.TelemetryConfig;

public static class TelemetryServiceConfiguration
{
    public const string FunctionsTelemetryModuleTypeName = "Microsoft.Azure.Functions.Worker.ApplicationInsights.FunctionsTelemetryModule";
    public const string WorkerApplicationInsightsLoggingEnabledCapability = "WorkerApplicationInsightsLoggingEnabled";

    public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.Configure<ApplicationInsightsServiceOptions>(options =>
        {
            options.EnableAdaptiveSampling = false;
        });

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Preserve the host-owned request graph instead of adding a second
        // worker-originating invocation dependency item.
        RemoveFunctionsInvocationTelemetryModule(services);

        // Read sampling settings from app settings / environment variables.
        // In Azure: change Configuration > Application Settings, then restart. No redeploy needed.
        // The sample currently uses rate-limited adaptive sampling for worker-originating telemetry.
        var samplingEnabled = context.Configuration.GetValue("Sampling:Enabled", true);
        var samplingPercentage = context.Configuration.GetValue("Sampling:Percentage", 100.0);
        var excludedTypes = context.Configuration.GetValue<string>("Sampling:ExcludedTypes");

        if (samplingEnabled)
        {
            services.Configure<TelemetryConfiguration>(config =>
            {
                var builder = config.DefaultTelemetrySink.TelemetryProcessorChainBuilder;

                builder.UseAdaptiveSampling(maxTelemetryItemsPerSecond: 5,
                    excludedTypes: excludedTypes);

                builder.UseAdaptiveSampling(maxTelemetryItemsPerSecond: 5,
                    includedTypes: "Event",
                    excludedTypes: null);

                builder.Build();
            });
        }

        services.AddSingleton(new SamplingSettings
        {
            Enabled = samplingEnabled,
            Percentage = samplingPercentage,
            ExcludedTypes = excludedTypes
        });
    }

    public static void RemoveFunctionsInvocationTelemetryModule(IServiceCollection services)
    {
        var functionsTelemetryModule = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(ITelemetryModule) &&
            descriptor.ImplementationType?.FullName == FunctionsTelemetryModuleTypeName);

        if (functionsTelemetryModule is not null)
        {
            services.Remove(functionsTelemetryModule);
        }
    }
}