namespace SharpClaw.RateLimiting;

/// <summary>
/// Feature flags for rate limiting migration.
/// </summary>
public sealed class RateLimitingFeatureFlags
{
    /// <summary>
    /// Enable the new System.Threading.RateLimiting implementation.
    /// Set to false to use legacy AspNetCoreRateLimit (deprecated).
    /// </summary>
    public bool UseNewRateLimiting { get; set; } = false;
    
    /// <summary>
    /// Allow fallback to legacy rate limiting if new implementation fails.
    /// </summary>
    public bool EnableFallback { get; set; } = true;
    
    /// <summary>
    /// Log metrics for rate limiting decisions (for debugging migration).
    /// </summary>
    public bool LogMigrationMetrics { get; set; } = true;
    
    /// <summary>
    /// Maintain backward-compatible HTTP 429 response format.
    /// When true, responses match legacy format including Retry-After header and message body.
    /// </summary>
    public bool UseLegacyResponseFormat { get; set; } = true;
    
    /// <summary>
    /// API version for rate limiting responses (for future breaking changes).
    /// Default is 1.0 for backward compatibility.
    /// </summary>
    public string ApiVersion { get; set; } = "1.0";
}

/// <summary>
/// Configuration options for rate limiting.
/// </summary>
public sealed class RateLimiterOptions
{
    /// <summary>
    /// Token bucket capacity (maximum burst size).
    /// </summary>
    public int TokenLimit { get; set; } = 100;
    
    /// <summary>
    /// Tokens added per replenishment period.
    /// </summary>
    public int TokensPerPeriod { get; set; } = 10;
    
    /// <summary>
    /// Time between token replenishments.
    /// </summary>
    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Whether to auto-replenish tokens.
    /// </summary>
    public bool AutoReplenishment { get; set; } = true;
    
    /// <summary>
    /// Bucket expiration time for cleanup.
    /// </summary>
    public TimeSpan BucketExpiration { get; set; } = TimeSpan.FromHours(1);
    
    /// <summary>
    /// Whether to reject requests when rate limited (true) or queue them (false).
    /// </summary>
    public bool RejectionBehavior { get; set; } = true;
}

/// <summary>
/// Per-endpoint rate limit options.
/// </summary>
public sealed class EndpointRateLimiterOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public int TokenLimit { get; set; } = 100;
    public int TokensPerPeriod { get; set; } = 10;
    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);
}
