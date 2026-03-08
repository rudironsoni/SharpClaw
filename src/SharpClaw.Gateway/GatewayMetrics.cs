using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SharpClaw.Gateway;

/// <summary>
/// Custom metrics for the SharpClaw Gateway.
/// </summary>
public sealed class GatewayMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _requestsCounter;
    private readonly Counter<long> _errorsCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly UpDownCounter<long> _activeConnections;
    private readonly Counter<long> _eventsPublishedCounter;
    private readonly Counter<long> _eventsForwardedCounter;

    public GatewayMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("SharpClaw.Gateway");
        
        _requestsCounter = _meter.CreateCounter<long>(
            "gateway.requests",
            unit: "{request}",
            description: "Total number of gateway requests");
        
        _errorsCounter = _meter.CreateCounter<long>(
            "gateway.errors",
            unit: "{error}",
            description: "Total number of gateway errors");
        
        _requestDuration = _meter.CreateHistogram<double>(
            "gateway.request.duration",
            unit: "s",
            description: "Gateway request duration in seconds");
        
        _activeConnections = _meter.CreateUpDownCounter<long>(
            "gateway.connections.active",
            unit: "{connection}",
            description: "Number of active connections");
        
        _eventsPublishedCounter = _meter.CreateCounter<long>(
            "gateway.events.published",
            unit: "{event}",
            description: "Total number of events published");
        
        _eventsForwardedCounter = _meter.CreateCounter<long>(
            "gateway.events.forwarded",
            unit: "{event}",
            description: "Total number of events forwarded");
    }

    public void RecordRequest(string method, string path, string tenantId, bool success)
    {
        var tags = new TagList
        {
            { "method", method },
            { "path", path },
            { "tenant_id", tenantId },
            { "success", success.ToString() }
        };
        _requestsCounter.Add(1, tags);
    }

    public void RecordError(string method, string path, string errorType)
    {
        var tags = new TagList
        {
            { "method", method },
            { "path", path },
            { "error_type", errorType }
        };
        _errorsCounter.Add(1, tags);
    }

    public void RecordRequestDuration(double durationSeconds, string method, string path)
    {
        var tags = new TagList
        {
            { "method", method },
            { "path", path }
        };
        _requestDuration.Record(durationSeconds, tags);
    }

    public void IncrementActiveConnections(string tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId } };
        _activeConnections.Add(1, tags);
    }

    public void DecrementActiveConnections(string tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId } };
        _activeConnections.Add(-1, tags);
    }

    public void RecordEventPublished(string eventType, string tenantId)
    {
        var tags = new TagList
        {
            { "event_type", eventType },
            { "tenant_id", tenantId }
        };
        _eventsPublishedCounter.Add(1, tags);
    }

    public void RecordEventForwarded(string eventType, bool success)
    {
        var tags = new TagList
        {
            { "event_type", eventType },
            { "success", success.ToString() }
        };
        _eventsForwardedCounter.Add(1, tags);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
