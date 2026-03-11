namespace QuestIFASampling.TelemetryConfig;

/// <summary>
/// Holds sampling settings read from IConfiguration at startup.
/// Change these via app settings and restart — no redeploy needed.
/// </summary>
public class SamplingSettings
{
    public bool Enabled { get; set; } = true;
    public double Percentage { get; set; } = 100.0;
    public string? ExcludedTypes { get; set; }
}
