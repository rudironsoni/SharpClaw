namespace SharpClaw.Host;

/// <summary>
/// Configuration options for health checks.
/// </summary>
public sealed class HealthCheckOptions
{
    /// <summary>
    /// Enable health checks.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval in seconds.
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Health check timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Health check endpoint path.
    /// </summary>
    public string Path { get; set; } = "/health";
}
