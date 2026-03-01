using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SharpClaw.Persistence.Contracts.Entities;

namespace SharpClaw.Persistence.Core.Configuration;

public sealed class DeviceIdentityConfiguration : IEntityTypeConfiguration<DeviceIdentityEntity>
{
    public void Configure(EntityTypeBuilder<DeviceIdentityEntity> builder)
    {
        builder.HasKey(e => new { e.TenantId, e.DeviceId });
        
        builder.HasIndex(e => new { e.TenantId, e.PublicKey })
            .IsUnique();
        
        builder.HasIndex(e => e.TenantId);
        
        builder.Property(e => e.DeviceId)
            .HasMaxLength(128);
        
        builder.Property(e => e.TenantId)
            .HasMaxLength(128);
        
        builder.Property(e => e.PublicKey)
            .HasMaxLength(4096);
        
        builder.Property(e => e.Scopes)
            .HasMaxLength(2048);
        
        builder.Property(e => e.DeviceName)
            .HasMaxLength(256);
        
        builder.Property(e => e.LastIpAddress)
            .HasMaxLength(45);
    }
}
