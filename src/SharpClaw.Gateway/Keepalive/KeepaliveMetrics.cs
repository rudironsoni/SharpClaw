using System.Diagnostics.Metrics;

namespace SharpClaw.Gateway.Keepalive;

/// <summary>
/// Telemetry metrics for keepalive monitoring.
/// </summary>
public sealed class KeepaliveMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _pingsSentCounter;
    private readonly Counter<long> _pongsReceivedCounter;
    private readonly Counter<long> _timeoutsDetectedCounter;
    private readonly Counter<long> _connectionsTerminatedCounter;
    private readonly Histogram<double> _pingResponseTimeHistogram;

    private readonly ConnectionHealthMonitor _healthMonitor;

    /// <summary>
    /// Creates a new KeepaliveMetrics instance.
    /// </summary>
    public KeepaliveMetrics(ConnectionHealthMonitor healthMonitor)
    {
        ArgumentNullException.ThrowIfNull(healthMonitor);

        _healthMonitor = healthMonitor;

        _meter = new Meter("SharpClaw.Gateway.Keepalive", "1.0.0");

        _pingsSentCounter = _meter.CreateCounter<long>(
            "sharpclaw.gateway.keepalive.pings_sent",
            description: "Total number of ping frames sent");

        _pongsReceivedCounter = _meter.CreateCounter<long>(
            "sharpclaw.gateway.keepalive.pongs_received",
            description: "Total number of pong responses received");

        _timeoutsDetectedCounter = _meter.CreateCounter<long>(
            "sharpclaw.gateway.keepalive.timeouts_detected",
            description: "Total number of connection timeouts detected");

        _connectionsTerminatedCounter = _meter.CreateCounter<long>(
            "sharpclaw.gateway.keepalive.connections_terminated",
            description: "Total number of connections terminated due to timeout");

        _pingResponseTimeHistogram = _meter.CreateHistogram<double>(
            "sharpclaw.gateway.keepalive.ping_response_time_ms",
            unit: "ms",
            description: "Response time for ping-pong round trips in milliseconds");
    }

    /// <summary>
    /// Records that a ping was sent.
    /// </summary>
    public void RecordPingSent()
    {
        _pingsSentCounter.Add(1);
    }

    /// <summary>
    /// Records that a pong was received.
    /// </summary>
    public void RecordPongReceived()
    {
        _pongsReceivedCounter.Add(1);
    }

    /// <summary>
    /// Records that a timeout was detected.
    /// </summary>
    public void RecordTimeoutDetected()
    {
        _timeoutsDetectedCounter.Add(1);
    }

    /// <summary>
    /// Records that a connection was terminated due to timeout.
    /// </summary>
    public void RecordConnectionTerminated()
    {
        _connectionsTerminatedCounter.Add(1);
    }

    /// <summary>
    /// Records the response time for a ping-pong round trip.
    /// </summary>
    public void RecordPingResponseTime(TimeSpan responseTime)
    {
        _pingResponseTimeHistogram.Record(responseTime.TotalMilliseconds);
    }

    /// <summary>
    /// Gets a snapshot of current keepalive statistics.
    /// </summary>
    public KeepaliveStatsSnapshot GetSnapshot()
    {
        var healthStates = _healthMonitor.GetAllHealthStates();
        var now = DateTimeOffset.UtcNow;

        int pendingPongCount = 0;
        int staleConnectionCount = 0;

        foreach (var state in healthStates.Values)
        {
            if (state.PendingPong)
            {
                pendingPongCount++;
            }

            if (state.ConsecutiveTimeouts > 0)
            {
                staleConnectionCount++;
            }
        }

        return new KeepaliveStatsSnapshot(
            Timestamp: now,
            MonitoredConnections: _healthMonitor.MonitoredCount,
            PendingPongs: pendingPongCount,
            StaleConnections: staleConnectionCount,
            TotalPingsSent: _healthMonitor.TotalPingsSent,
            TotalPongsReceived: _healthMonitor.TotalPongsReceived,
            TotalTimeoutsDetected: _healthMonitor.TotalTimeoutsDetected,
            AverageResponseTimeMs: CalculateAverageResponseTime(healthStates, now));
    }

    private static double CalculateAverageResponseTime(
        IReadOnlyDictionary<string, ConnectionHealthState> healthStates,
        DateTimeOffset now)
    {
        var responseTimes = healthStates.Values
            .Where(s => s.LastPingSent.HasValue && !s.PendingPong)
            .Select(s => (now - s.LastPingSent!.Value).TotalMilliseconds)
            .ToList();

        return responseTimes.Count > 0
            ? responseTimes.Average()
            : 0.0;
    }

    /// <summary>
    /// Disposes the metrics meter.
    /// </summary>
    public void Dispose()
    {
        _meter.Dispose();
    }
}

/// <summary>
/// Snapshot of keepalive statistics at a point in time.
/// </summary>
public sealed record KeepaliveStatsSnapshot(
    DateTimeOffset Timestamp,
    int MonitoredConnections,
    int PendingPongs,
    int StaleConnections,
    long TotalPingsSent,
    long TotalPongsReceived,
    long TotalTimeoutsDetected,
    double AverageResponseTimeMs);
