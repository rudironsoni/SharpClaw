using Microsoft.EntityFrameworkCore;
using SharpClaw.Persistence.Contracts.Entities;
using SharpClaw.Persistence.Core.Configuration;
using SharpClaw.Tenancy.Abstractions;

namespace SharpClaw.Persistence.Core;

/// <summary>
/// Entity Framework Core database context with multi-tenant support.
/// </summary>
public sealed class SharpClawDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DbSet<DeviceIdentityEntity> DeviceIdentities => Set<DeviceIdentityEntity>();
    public DbSet<RunRecordEntity> RunRecords => Set<RunRecordEntity>();
    public DbSet<SessionRecordEntity> Sessions => Set<SessionRecordEntity>();
    public DbSet<ConfigRevisionEntity> ConfigRevisions => Set<ConfigRevisionEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<IdempotencyKeyEntity> IdempotencyKeys => Set<IdempotencyKeyEntity>();

    public SharpClawDbContext(
        DbContextOptions<SharpClawDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new DeviceIdentityConfiguration());
        modelBuilder.ApplyConfiguration(new RunRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SessionConfiguration());
        modelBuilder.ApplyConfiguration(new ConfigRevisionConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyKeyConfiguration());
    }

    public override int SaveChanges()
    {
        SetTenantIds();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTenantIds();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTenantIds()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.TenantId = _tenantContext.TenantId;
            }
        }
    }
}

/// <summary>
/// Marker interface for tenant-scoped entities.
/// </summary>
public interface ITenantScoped
{
    string TenantId { get; set; }
}
