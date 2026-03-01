using System.Linq.Expressions;

namespace SharpClaw.Abstractions.Persistence;

/// <summary>
/// Generic repository interface.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetAsync(string id, string tenantId, CancellationToken ct = default);
    Task<T?> GetByExpressionAsync(Expression<Func<T, bool>> predicate, string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate, string tenantId, CancellationToken ct = default);
    Task UpsertAsync(string id, string tenantId, T entity, CancellationToken ct = default);
    Task DeleteAsync(string id, string tenantId, CancellationToken ct = default);
    IQueryable<T> Query(string tenantId);
}

/// <summary>
/// Unit of work interface.
/// </summary>
public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

/// <summary>
/// Idempotency entry.
/// </summary>
public sealed record IdempotencyEntry
{
    public required string Key { get; init; }
    public required string TenantId { get; init; }
    public required string RunId { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Idempotency storage interface.
/// </summary>
public interface IIdempotencyStorage
{
    Task<IdempotencyEntry?> GetAsync(string key, string tenantId, CancellationToken ct = default);
    Task SetAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken ct = default);
}
