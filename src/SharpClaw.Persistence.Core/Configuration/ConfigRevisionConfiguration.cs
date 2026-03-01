using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Persistence.Core.Configuration;

public sealed class ConfigRevisionConfiguration : IEntityTypeConfiguration<ConfigRevisionEntity>
{
    public void Configure(EntityTypeBuilder<ConfigRevisionEntity> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.RevisionId });
        
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.IsActive });
        
        builder.Property(e => e.RevisionId)
            .HasMaxLength(128);
        
        builder.Property(e => e.TenantId)
            .HasMaxLength(128);
        
        builder.Property(e => e.Hash)
            .HasMaxLength(64);
        
        builder.Property(e => e.CreatedBy)
            .HasMaxLength(128);
        
        builder.Property(e => e.ChangeDescription)
            .HasMaxLength(512);
    }
}
