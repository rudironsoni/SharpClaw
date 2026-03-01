using System.Threading.Channels;

namespace SharpClaw.Abstractions.Runs;

/// <summary>
/// Run status.
/// </summary>
public enum RunStatus
{
    Pending,
    Started,
    Running,
    Completed,
    Failed,
    Aborted
}

/// <summary>
/// Run event.
/// </summary>
public sealed record RunEvent
{
    public required string Event { get; init; }
    public required string RunId { get; init; }
    public object? Payload { get; init; }
    public long Sequence { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Run input.
/// </summary>
public sealed record RunInput
{
    public required string Model { get; init; }
    public required string Input { get; init; }
    public string? ConversationId { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Run start result.
/// </summary>
public sealed record RunStartResult
{
    public required string RunId { get; init; }
    public required string Status { get; init; }
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// Run snapshot.
/// </summary>
public sealed record RunSnapshot
{
    public required string RunId { get; init; }
    public required string TenantId { get; init; }
    public required string Status { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? Provider { get; init; }
}

/// <summary>
/// Run coordinator interface.
/// </summary>
public interface IRunCoordinator
{
    Task<RunStartResult> StartAsync(RunInput input, string tenantId, string? idempotencyKey = null, CancellationToken ct = default);
    Task AbortAsync(string runId, string tenantId, CancellationToken ct = default);
    Task<RunSnapshot> GetSnapshotAsync(string runId, string tenantId, CancellationToken ct = default);
    IAsyncEnumerable<RunEvent> ReadEventsAsync(string runId, string tenantId, CancellationToken ct = default);
}

/// <summary>
/// Run middleware context.
/// </summary>
public sealed class RunContext
{
    public required string RunId { get; init; }
    public required string TenantId { get; init; }
    public required RunInput Input { get; init; }
    public required ChannelWriter<RunEvent> EventWriter { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Run middleware.
/// </summary>
public interface IRunMiddleware
{
    Task InvokeAsync(RunContext context, Func<Task> next);
}

/// <summary>
/// Run pipeline.
/// </summary>
public interface IRunPipeline
{
    void Use(IRunMiddleware middleware);
    Task ExecuteAsync(RunContext context);
}
