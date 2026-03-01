using System.Collections.Concurrent;

namespace SharpClaw.Observability.Logging;

/// <summary>
/// Provides access to the current correlation ID for request tracking.
/// </summary>
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
    void SetCorrelationId(string correlationId);
    void Clear();
}

/// <summary>
/// AsyncLocal-based correlation ID accessor that flows across async boundaries.
/// </summary>
public sealed class AsyncLocalCorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    public string? CorrelationId => _correlationId.Value;

    public void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
    }

    public void Clear()
    {
        _correlationId.Value = null;
    }
}

/// <summary>
/// In-memory correlation ID accessor for testing.
/// </summary>
public sealed class InMemoryCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly ConcurrentDictionary<int, string> _ids = new();

    public string? CorrelationId
    {
        get
        {
            _ids.TryGetValue(Environment.CurrentManagedThreadId, out var id);
            return id;
        }
    }

    public void SetCorrelationId(string correlationId)
    {
        _ids[Environment.CurrentManagedThreadId] = correlationId;
    }

    public void Clear()
    {
        _ids.TryRemove(Environment.CurrentManagedThreadId, out _);
    }
}
