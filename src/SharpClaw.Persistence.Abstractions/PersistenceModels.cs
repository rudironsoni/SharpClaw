namespace SharpClaw.Persistence.Abstractions;

public enum RunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    Interrupted
}

public sealed record RunRecord(
    string RunId,
    RunStatus Status,
    DateTimeOffset UpdatedAt,
    string? IdempotencyKey = null,
    string? LastError = null);

public sealed record SessionRecord(
    string SessionId,
    string Scope,
    DateTimeOffset UpdatedAt,
    string? LastMessageId = null);

public sealed record ConfigRevisionRecord(
    string Revision,
    string Hash,
    DateTimeOffset UpdatedAt,
    bool IsActive);

public sealed record AuditEventRecord(
    string EventId,
    string Category,
    string Action,
    DateTimeOffset OccurredAt,
    string? Actor = null,
    string? Target = null,
    string? Detail = null);
