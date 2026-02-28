using System.Collections.Concurrent;
using SharpClaw.Persistence.Abstractions;

namespace SharpClaw.Persistence.Sqlite;

public sealed class SqliteRunStore : IRunStore
{
    private readonly ConcurrentDictionary<string, RunRecord> _runs = new(StringComparer.Ordinal);

    public Task<RunRecord?> GetAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();

        _runs.TryGetValue(runId, out var record);
        return Task.FromResult(record);
    }

    public Task UpsertAsync(RunRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        _runs[record.RunId] = record;
        return Task.CompletedTask;
    }
}

public sealed class SqliteSessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new(StringComparer.Ordinal);

    public Task<SessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        _sessions.TryGetValue(sessionId, out var record);
        return Task.FromResult(record);
    }

    public Task UpsertAsync(SessionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        _sessions[record.SessionId] = record;
        return Task.CompletedTask;
    }
}

public sealed class SqliteConfigRevisionStore : IConfigRevisionStore
{
    private readonly ConcurrentDictionary<string, ConfigRevisionRecord> _revisions = new(StringComparer.Ordinal);

    public Task<ConfigRevisionRecord?> GetAsync(string revision, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        cancellationToken.ThrowIfCancellationRequested();

        _revisions.TryGetValue(revision, out var record);
        return Task.FromResult(record);
    }

    public Task UpsertAsync(ConfigRevisionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        _revisions[record.Revision] = record;
        return Task.CompletedTask;
    }
}

public sealed class SqliteAuditStore : IAuditStore
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<AuditEventRecord>> _byCategory =
        new(StringComparer.OrdinalIgnoreCase);

    public Task AppendAsync(AuditEventRecord auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();

        var queue = _byCategory.GetOrAdd(auditEvent.Category, _ => new ConcurrentQueue<AuditEventRecord>());
        queue.Enqueue(auditEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEventRecord>> ListByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_byCategory.TryGetValue(category, out var queue))
        {
            return Task.FromResult<IReadOnlyList<AuditEventRecord>>([]);
        }

        return Task.FromResult<IReadOnlyList<AuditEventRecord>>([.. queue]);
    }
}
