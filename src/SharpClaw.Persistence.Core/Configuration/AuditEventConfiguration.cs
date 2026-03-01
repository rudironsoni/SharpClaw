using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Persistence.Core.Configuration;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventEntity>
{
    public void Configure(EntityTypeBuilder<AuditEventEntity> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.EventId });
        
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.Timestamp });
        builder.HasIndex(e => new { e.TenantId, e.EntityType });
        builder.HasIndex(e => new { e.TenantId, e.Action });
        
        builder.Property(e => e.EventId)
            .HasMaxLength(128);
        
        builder.Property(e => e.TenantId)
            .HasMaxLength(128);
        
        builder.Property(e => e.EntityType)
            .HasMaxLength(128);
        
        builder.Property(e => e.Action)
            .HasMaxLength(50);
        
        builder.Property(e => e.EntityId)
            .HasMaxLength(128);
        
        builder.Property(e => e.UserId)
            .HasMaxLength(128);
        
        builder.Property(e => e.OldValues);
        
        builder.Property(e => e.NewValues);
        
        builder.Property(e => e.Metadata)
            .HasMaxLength(2048);
    }
}
