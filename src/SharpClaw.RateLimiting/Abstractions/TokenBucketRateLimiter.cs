namespace SharpClaw.RateLimiting.Abstractions;

/// <summary>
/// Token bucket rate limiter implementation.
/// </summary>
public sealed class TokenBucketRateLimiter : IRateLimiter
{
    private readonly IRateLimitStore _store;
    private readonly RateLimiterOptions _options;

    public TokenBucketRateLimiter(IRateLimitStore store, RateLimiterOptions options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public RateLimitLease Acquire(string key, int permitCount = 1)
    {
        var entry = _store.GetOrCreate(key, () => new TokenBucketEntry
        {
            Tokens = _options.TokenLimit,
            LastReplenished = DateTime.UtcNow
        });

        lock (entry)
        {
            ReplenishTokens(entry);

            if (entry.Tokens >= permitCount)
        {
            entry.Tokens -= permitCount;
            return new TokenBucketLease(true, null);
        }
        return new TokenBucketLease(false, CalculateRetryAfter(entry));
        }
    }

    public ValueTask<RateLimitLease> AcquireAsync(string key, int permitCount = 1, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Acquire(key, permitCount));
    }

    public RateLimiterStatsSnapshot GetStatistics(string key)
    {
        var entry = _store.GetOrCreate(key, () => new TokenBucketEntry
        {
            Tokens = _options.TokenLimit,
            LastReplenished = DateTime.UtcNow
        });

        lock (entry)
        {
            ReplenishTokens(entry);
            return new RateLimiterStatsSnapshot
            {
                CurrentAvailablePermits = (long)entry.Tokens,
                TotalSuccessfulLeases = 0, // Would track in real implementation
                TotalFailedLeases = 0,
                CurrentQueuedCount = 0
            };
        }
    }

    private void ReplenishTokens(TokenBucketEntry entry)
    {
        var now = DateTime.UtcNow;
        var timeSinceLastReplenish = now - entry.LastReplenished;
        var periodsElapsed = (long)(timeSinceLastReplenish / _options.ReplenishmentPeriod);

        if (periodsElapsed > 0)
        {
            entry.Tokens = Math.Min(
                _options.TokenLimit,
                entry.Tokens + (periodsElapsed * _options.TokensPerPeriod));
            entry.LastReplenished = now;
        }
    }

    private TimeSpan? CalculateRetryAfter(TokenBucketEntry entry)
    {
        if (entry.Tokens >= 1) return null;

        var tokensNeeded = 1 - entry.Tokens;
        var periodsNeeded = (long)Math.Ceiling(tokensNeeded / (double)_options.TokensPerPeriod);
        return TimeSpan.FromTicks(_options.ReplenishmentPeriod.Ticks * periodsNeeded);
    }
}

/// <summary>
/// Token bucket lease implementation.
/// </summary>
public sealed class TokenBucketLease : RateLimitLease
{
    public override bool IsAcquired { get; }
    public override TimeSpan? RetryAfter { get; }

    public TokenBucketLease(bool isAcquired, TimeSpan? retryAfter)
    {
        IsAcquired = isAcquired;
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Token bucket entry for storage.
/// </summary>
public sealed class TokenBucketEntry
{
    public long Tokens { get; set; }
    public DateTime LastReplenished { get; set; }
}
