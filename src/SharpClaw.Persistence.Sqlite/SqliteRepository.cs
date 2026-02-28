using System.Collections.Concurrent;
using SharpClaw.Persistence.Abstractions;

namespace SharpClaw.Persistence.Sqlite;

public sealed class SqliteRepository<T> : IRepository<T>
{
    private readonly ConcurrentDictionary<string, T> _items = new(StringComparer.Ordinal);

    public Task<T?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        _items.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task UpsertAsync(string id, T item, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        _items[id] = item;
        return Task.CompletedTask;
    }
}
