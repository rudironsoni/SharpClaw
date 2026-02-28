namespace SharpClaw.Persistence.Abstractions;

public interface IRunStore
{
    Task<RunRecord?> GetAsync(string runId, CancellationToken cancellationToken = default);

    Task UpsertAsync(RunRecord record, CancellationToken cancellationToken = default);
}

public interface ISessionStore
{
    Task<SessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    Task UpsertAsync(SessionRecord record, CancellationToken cancellationToken = default);
}

public interface IConfigRevisionStore
{
    Task<ConfigRevisionRecord?> GetAsync(string revision, CancellationToken cancellationToken = default);

    Task UpsertAsync(ConfigRevisionRecord record, CancellationToken cancellationToken = default);
}

public interface IAuditStore
{
    Task AppendAsync(AuditEventRecord auditEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEventRecord>> ListByCategoryAsync(string category, CancellationToken cancellationToken = default);
}
