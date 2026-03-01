using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Persistence.Core.Configuration;

public sealed class SessionConfiguration : IEntityTypeConfiguration<SessionRecordEntity>
{
    public void Configure(EntityTypeBuilder<SessionRecordEntity> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.SessionId });
        
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.DeviceId });
        builder.HasIndex(e => new { e.TenantId, e.IsActive });
        builder.HasIndex(e => e.ExpiresAt);
        
        builder.Property(e => e.SessionId)
            .HasMaxLength(128);
        
        builder.Property(e => e.TenantId)
            .HasMaxLength(128);
        
        builder.Property(e => e.DeviceId)
            .HasMaxLength(128);
        
        builder.Property(e => e.IpAddress)
            .HasMaxLength(45);
        
        builder.Property(e => e.UserAgent)
            .HasMaxLength(512);
    }
}