using System.Diagnostics.Metrics;

namespace SharpClaw.Execution.Docker;

/// <summary>
/// Custom metrics for Docker execution sandbox.
/// </summary>
public sealed class ExecutionMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _executionsCounter;
    private readonly Counter<long> _executionErrorsCounter;
    private readonly Histogram<double> _executionDuration;
    private readonly Histogram<long> _memoryUsage;
    private readonly Histogram<double> _cpuUsage;
    private readonly UpDownCounter<long> _activeContainers;

    public ExecutionMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("SharpClaw.Execution.Docker");
        
        _executionsCounter = _meter.CreateCounter<long>(
            "execution.total",
            unit: "{execution}",
            description: "Total number of code executions");
        
        _executionErrorsCounter = _meter.CreateCounter<long>(
            "execution.errors",
            unit: "{error}",
            description: "Total number of execution errors");
        
        _executionDuration = _meter.CreateHistogram<double>(
            "execution.duration",
            unit: "s",
            description: "Execution duration in seconds");
        
        _memoryUsage = _meter.CreateHistogram<long>(
            "execution.memory.usage",
            unit: "By",
            description: "Memory usage in bytes");
        
        _cpuUsage = _meter.CreateHistogram<double>(
            "execution.cpu.usage",
            unit: "{cpu}",
            description: "CPU usage percentage");
        
        _activeContainers = _meter.CreateUpDownCounter<long>(
            "execution.containers.active",
            unit: "{container}",
            description: "Number of active containers");
    }

    public void RecordExecution(string language, string tenantId, bool success)
    {
        var tags = new TagList
        {
            { "language", language },
            { "tenant_id", tenantId },
            { "success", success.ToString() }
        };
        _executionsCounter.Add(1, tags);
    }

    public void RecordExecutionError(string language, string errorType)
    {
        var tags = new TagList
        {
            { "language", language },
            { "error_type", errorType }
        };
        _executionErrorsCounter.Add(1, tags);
    }

    public void RecordExecutionDuration(double durationSeconds, string language)
    {
        var tags = new TagList { { "language", language } };
        _executionDuration.Record(durationSeconds, tags);
    }

    public void RecordMemoryUsage(long bytes, string language)
    {
        var tags = new TagList { { "language", language } };
        _memoryUsage.Record(bytes, tags);
    }

    public void RecordCpuUsage(double percentage, string language)
    {
        var tags = new TagList { { "language", language } };
        _cpuUsage.Record(percentage, tags);
    }

    public void IncrementActiveContainers(string tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId } };
        _activeContainers.Add(1, tags);
    }

    public void DecrementActiveContainers(string tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId } };
        _activeContainers.Add(-1, tags);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
