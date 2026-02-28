namespace SharpClaw.Persistence.Abstractions;

public interface IRepository<T>
{
    Task<T?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertAsync(string id, T item, CancellationToken cancellationToken = default);
}
