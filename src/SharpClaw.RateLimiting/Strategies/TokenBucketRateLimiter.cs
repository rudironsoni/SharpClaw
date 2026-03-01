using Microsoft.Extensions.Logging;
using SharpClaw.RateLimiting.Abstractions;

namespace SharpClaw.RateLimiting.Strategies;

/// <summary>
/// Token bucket rate limiter implementation.
/// </summary>
public sealed class TokenBucketRateLimiter : IRateLimiter
{
    private readonly IRateLimitStore _store;
    private readonly ILogger<TokenBucketRateLimiter> _logger;
    
    public TokenBucketRateLimiter(
        IRateLimitStore store,
        ILogger<TokenBucketRateLimiter> logger)
    {
        _store = store;
        _logger = logger;
    }
    
    public RateLimitLease Acquire(string key, int permitCount = 1)
    {
        var bucket = _store.GetOrCreateBucket(key, GetDefaultOptions());
        
        lock (bucket)
        {
            ReplenishTokens(bucket);
            
            if (bucket.Tokens >= permitCount)
            {
                bucket.Tokens -= permitCount;
                _logger.LogDebug(
                    "Rate limit permit acquired for {Key}. Remaining: {Remaining}",
                    key, bucket.Tokens);
                return new TokenBucketLease(true, TimeSpan.Zero);
            }
            
            var retryAfter = CalculateRetryAfter(bucket);
            _logger.LogWarning(
                "Rate limit exceeded for {Key}. Retry after: {RetryAfter}",
                key, retryAfter);
            return new TokenBucketLease(false, retryAfter);
        }
    }
    
    public ValueTask<RateLimitLease> AcquireAsync(string key, int permitCount = 1, CancellationToken cancellationToken = default)
    {
        return new ValueTask<RateLimitLease>(Acquire(key, permitCount));
    }
    
    public RateLimiterStatsSnapshot GetStatistics(string key)
    {
        var bucket = _store.GetOrCreateBucket(key, GetDefaultOptions());
        
        lock (bucket)
        {
            ReplenishTokens(bucket);
            
            return new RateLimiterStatsSnapshot
            {
                CurrentAvailablePermits = bucket.Tokens,
                CurrentQueuedCount = 0
            };
        }
    }
    
    private void ReplenishTokens(RateLimitBucket bucket)
    {
        if (!bucket.Options.AutoReplenishment)
            return;
        
        var now = DateTimeOffset.UtcNow;
        var timeSinceLastReplenishment = now - bucket.LastReplenishment;
        var periodsPassed = (long)(timeSinceLastReplenishment / bucket.Options.ReplenishmentPeriod);
        
        if (periodsPassed > 0)
        {
            var tokensToAdd = periodsPassed * bucket.Options.TokensPerPeriod;
            bucket.Tokens = Math.Min(bucket.Options.TokenLimit, bucket.Tokens + tokensToAdd);
            bucket.LastReplenishment = now;
        }
    }
    
    private TimeSpan CalculateRetryAfter(RateLimitBucket bucket)
    {
        var tokensNeeded = 1;
        var periodsNeeded = (int)Math.Ceiling((double)tokensNeeded / bucket.Options.TokensPerPeriod);
        return bucket.Options.ReplenishmentPeriod * periodsNeeded;
    }
    
    private static TokenBucketOptions GetDefaultOptions()
    {
        return new TokenBucketOptions
        {
            TokenLimit = 100,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true
        };
    }
}

/// <summary>
/// Token bucket lease implementation.
/// </summary>
internal sealed class TokenBucketLease : RateLimitLease
{
    private readonly TimeSpan _retryAfter;
    
    public TokenBucketLease(bool isAcquired, TimeSpan retryAfter)
    {
        IsAcquired = isAcquired;
        _retryAfter = retryAfter;
    }
    
    public override bool IsAcquired { get; }
    
    public override TimeSpan? RetryAfter => _retryAfter > TimeSpan.Zero ? _retryAfter : null;
}
