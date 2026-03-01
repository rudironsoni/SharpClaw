using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Persistence.Core.Configuration;

public sealed class RunRecordConfiguration : IEntityTypeConfiguration<RunRecordEntity>
{
    public void Configure(EntityTypeBuilder<RunRecordEntity> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.RunId });
        
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
        
        builder.Property(e => e.RunId)
            .HasMaxLength(128);
        
        builder.Property(e => e.TenantId)
            .HasMaxLength(128);
        
        builder.Property(e => e.IdempotencyKey)
            .HasMaxLength(128);
        
        builder.Property(e => e.Status)
            .HasMaxLength(50);
        
        builder.Property(e => e.Provider)
            .HasMaxLength(50);
        
        builder.Property(e => e.SandboxId)
            .HasMaxLength(256);
        
        builder.Property(e => e.DeviceId)
            .HasMaxLength(128);
    }
}
