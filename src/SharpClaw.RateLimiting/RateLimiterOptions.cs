namespace SharpClaw.RateLimiting;

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
