using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SharpClaw.Abstractions.Persistence;
using SharpClaw.Persistence.Core;

namespace SharpClaw.Persistence.Sqlite.Repositories;

public sealed class SqliteRepository<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly SharpClawDbContext _context;

    public SqliteRepository(SharpClawDbContext context)
    {
        _context = context;
    }

    public async Task<TEntity?> GetAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<TEntity>().FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<TEntity?> GetByExpressionAsync(
        Expression<Func<TEntity, bool>> predicate, 
        string tenantId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<TEntity>().FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<TEntity>().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>> predicate, 
        string tenantId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<TEntity>().Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(
        string id, 
        string tenantId, 
        TEntity entity, 
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.Set<TEntity>().FindAsync(new object[] { id }, cancellationToken);
        if (existing == null)
        {
            _context.Set<TEntity>().Add(entity);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<TEntity>().FindAsync(new object[] { id }, cancellationToken);
        if (entity != null)
        {
            _context.Set<TEntity>().Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public IQueryable<TEntity> Query(string tenantId) => _context.Set<TEntity>().AsQueryable();
}
