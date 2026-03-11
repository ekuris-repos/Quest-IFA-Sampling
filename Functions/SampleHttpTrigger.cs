using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QuestIFASampling.TelemetryConfig;

namespace QuestIFASampling.Functions;

public class SampleHttpTrigger
{
    private readonly ILogger<SampleHttpTrigger> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly SamplingSettings _samplingSettings;

    public SampleHttpTrigger(
        ILogger<SampleHttpTrigger> logger,
        TelemetryClient telemetryClient,
        SamplingSettings samplingSettings)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
        _samplingSettings = samplingSettings;
    }

    [Function("SampleHttpTrigger")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        // Generate custom telemetry to demonstrate sampling behavior
        _telemetryClient.TrackEvent("SampleEventProcessed", new Dictionary<string, string>
        {
            { "RequestMethod", req.Method },
            { "Timestamp", DateTime.UtcNow.ToString("o") }
        });

        _telemetryClient.TrackTrace("Sample trace message for sampling demo", SeverityLevel.Information);

        return new OkObjectResult(new
        {
            Message = "Telemetry generated successfully.",
            SamplingEnabled = _samplingSettings.Enabled,
            SamplingPercentage = _samplingSettings.Percentage,
            ExcludedTypes = _samplingSettings.ExcludedTypes
        });
    }

    [Function("GenerateTelemetry")]
    public IActionResult GenerateTelemetry(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        var count = int.TryParse(req.Query["count"], out var c) ? c : 10;
        count = Math.Min(count, 1000); // cap to prevent abuse

        _logger.LogInformation("Generating {Count} telemetry items.", count);

        for (int i = 0; i < count; i++)
        {
            _telemetryClient.TrackEvent($"BulkEvent_{i}", new Dictionary<string, string>
            {
                { "Index", i.ToString() },
                { "BatchTimestamp", DateTime.UtcNow.ToString("o") }
            });

            _telemetryClient.TrackTrace($"Bulk trace {i}", SeverityLevel.Verbose);
        }

        _telemetryClient.Flush();

        return new OkObjectResult(new
        {
            Message = $"Generated {count} telemetry events and traces.",
            SamplingEnabled = _samplingSettings.Enabled,
            SamplingPercentage = _samplingSettings.Percentage,
            ExcludedTypes = _samplingSettings.ExcludedTypes
        });
    }

    [Function("SamplingStatus")]
    public IActionResult GetSamplingStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        return new OkObjectResult(new
        {
            SamplingEnabled = _samplingSettings.Enabled,
            SamplingPercentage = _samplingSettings.Percentage,
            ExcludedTypes = _samplingSettings.ExcludedTypes,
            Note = "Change Sampling__Enabled, Sampling__Percentage, or Sampling__ExcludedTypes in app settings, then restart the Function App. No redeploy needed."
        });
    }
}
