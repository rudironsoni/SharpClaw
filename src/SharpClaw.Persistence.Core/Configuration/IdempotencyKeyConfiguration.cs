using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Persistence.Core.Configuration;

public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntity> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.Key });
        
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.ExpiresAt });
        
        builder.Property(e => e.Key)
            .HasMaxLength(256);
        
        builder.Property(e => e.TenantId)
            .HasMaxLength(128);
        
        builder.Property(e => e.RunId)
            .HasMaxLength(128);
        
        builder.Property(e => e.Status)
            .HasMaxLength(32);
    }
}
