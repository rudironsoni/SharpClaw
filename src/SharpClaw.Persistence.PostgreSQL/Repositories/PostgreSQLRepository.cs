using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SharpClaw.Abstractions.Persistence;
using SharpClaw.Persistence.Core;

namespace SharpClaw.Persistence.PostgreSQL.Repositories;

public sealed class PostgreSQLRepository<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly SharpClawDbContext _context;
    private readonly DbSet<TEntity> _dbSet;

    public PostgreSQLRepository(SharpClawDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public async Task<TEntity?> GetAsync(
        string id,
        string tenantId,
        CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(new object[] { tenantId, id }, ct);
    }

    public async Task<TEntity?> GetByExpressionAsync(
        Expression<Func<TEntity, bool>> predicate,
        string tenantId,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Where(predicate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        return await _dbSet.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>> predicate,
        string tenantId,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Where(predicate)
            .ToListAsync(ct);
    }

    public async Task UpsertAsync(
        string id,
        string tenantId,
        TEntity entity,
        CancellationToken ct = default)
    {
        var existing = await _dbSet.FindAsync(new object[] { tenantId, id }, ct);
        if (existing == null)
        {
            await _dbSet.AddAsync(entity, ct);
        }
        else
        {
            _dbSet.Entry(existing).CurrentValues.SetValues(entity);
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        string id,
        string tenantId,
        CancellationToken ct = default)
    {
        var entity = await _dbSet.FindAsync(new object[] { tenantId, id }, ct);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync(ct);
        }
    }

    public IQueryable<TEntity> Query(string tenantId)
    {
        return _dbSet.AsQueryable();
    }
}
