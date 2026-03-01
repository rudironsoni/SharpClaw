namespace SharpClaw.RateLimiting.Abstractions;

/// <summary>
/// Rate limiter interface for controlling request rates.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Attempts to acquire a permit for the specified key.
    /// </summary>
    RateLimitLease Acquire(string key, int permitCount = 1);
    
    /// <summary>
    /// Attempts to acquire a permit asynchronously.
    /// </summary>
    ValueTask<RateLimitLease> AcquireAsync(string key, int permitCount = 1, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current statistics for a key.
    /// </summary>
    RateLimiterStatsSnapshot GetStatistics(string key);
}

/// <summary>
/// Represents a lease for rate-limited resources.
/// </summary>
public abstract class RateLimitLease : IDisposable
{
    /// <summary>
    /// Gets whether the lease was successfully acquired.
    /// </summary>
    public abstract bool IsAcquired { get; }
    
    /// <summary>
    /// Gets the retry after duration if the lease was not acquired.
    /// </summary>
    public virtual TimeSpan? RetryAfter { get; }
    
    /// <summary>
    /// Disposes the lease.
    /// </summary>
    public virtual void Dispose() { }
}

/// <summary>
/// Rate limiter statistics snapshot.
/// </summary>
public sealed class RateLimiterStatsSnapshot
{
    public long TotalSuccessfulLeases { get; init; }
    public long TotalFailedLeases { get; init; }
    public long CurrentAvailablePermits { get; init; }
    public long CurrentQueuedCount { get; init; }
}
