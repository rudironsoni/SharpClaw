namespace SharpClaw.RateLimiting.Abstractions;

/// <summary>
/// Store for rate limit bucket state.
/// </summary>
public interface IRateLimitStore
{
    /// <summary>
    /// Gets or creates a bucket for the specified key.
    /// </summary>
    RateLimitBucket GetOrCreateBucket(string key, TokenBucketOptions options);
    
    /// <summary>
    /// Attempts to remove a bucket.
    /// </summary>
    bool TryRemoveBucket(string key);
    
    /// <summary>
    /// Gets all bucket keys.
    /// </summary>
    IEnumerable<string> GetBucketKeys();
}

/// <summary>
/// Rate limit bucket state.
/// </summary>
public sealed class RateLimitBucket
{
    public long Tokens { get; set; }
    public DateTimeOffset LastReplenishment { get; set; }
    public TokenBucketOptions Options { get; init; } = null!;
}

/// <summary>
/// Token bucket options.
/// </summary>
public sealed class TokenBucketOptions
{
    public int TokenLimit { get; init; }
    public int TokensPerPeriod { get; init; }
    public TimeSpan ReplenishmentPeriod { get; init; }
    public bool AutoReplenishment { get; init; } = true;
}
