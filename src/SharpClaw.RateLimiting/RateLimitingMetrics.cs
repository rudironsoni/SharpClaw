using System.Diagnostics.Metrics;

namespace SharpClaw.RateLimiting;

/// <summary>
/// Custom metrics for rate limiting.
/// </summary>
public sealed class RateLimitingMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _requestsAllowedCounter;
    private readonly Counter<long> _requestsThrottledCounter;
    private readonly Histogram<double> _tokensAvailable;
    private readonly UpDownCounter<long> _activeLeases;
    private readonly Counter<long> _leaseAcquisitionTime;

    public RateLimitingMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("SharpClaw.RateLimiting");
        
        _requestsAllowedCounter = _meter.CreateCounter<long>(
            "ratelimit.requests.allowed",
            unit: "{request}",
            description: "Total number of requests allowed");
        
        _requestsThrottledCounter = _meter.CreateCounter<long>(
            "ratelimit.requests.throttled",
            unit: "{request}",
            description: "Total number of requests throttled");
        
        _tokensAvailable = _meter.CreateHistogram<double>(
            "ratelimit.tokens.available",
            unit: "{token}",
            description: "Number of available tokens when request arrives");
        
        _activeLeases = _meter.CreateUpDownCounter<long>(
            "ratelimit.leases.active",
            unit: "{lease}",
            description: "Number of active rate limit leases");
        
        _leaseAcquisitionTime = _meter.CreateCounter<long>(
            "ratelimit.lease.acquisition.time",
            unit: "ms",
            description: "Time to acquire rate limit lease in milliseconds");
    }

    public void RecordRequestAllowed(string tenantId, string deviceId)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId },
            { "device_id", deviceId }
        };
        _requestsAllowedCounter.Add(1, tags);
    }

    public void RecordRequestThrottled(string tenantId, string deviceId, string reason)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId },
            { "device_id", deviceId },
            { "reason", reason }
        };
        _requestsThrottledCounter.Add(1, tags);
    }

    public void RecordTokensAvailable(double tokens, string tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId } };
        _tokensAvailable.Record(tokens, tags);
    }

    public void IncrementActiveLeases(string tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId } };
        _activeLeases.Add(1, tags);
    }

    public void DecrementActiveLeases(string tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId } };
        _activeLeases.Add(-1, tags);
    }

    public void RecordLeaseAcquisitionTime(long milliseconds, string tenantId, bool success)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId },
            { "success", success.ToString() }
        };
        _leaseAcquisitionTime.Add(milliseconds, tags);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
