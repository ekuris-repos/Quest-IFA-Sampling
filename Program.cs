using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestIFASampling.TelemetryConfig;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Read sampling settings from app settings / environment variables.
        // In Azure: change Configuration > Application Settings, then restart. No redeploy needed.
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

        // Register sampling settings so they can be read by functions for diagnostics
        services.AddSingleton(new SamplingSettings
        {
            Enabled = samplingEnabled,
            Percentage = samplingPercentage,
            ExcludedTypes = excludedTypes
        });
    })
    .Build();

host.Run();
