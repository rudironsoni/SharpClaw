using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SharpClaw.Persistence.Contracts.Entities;

/// <summary>
/// Device identity entity for EF Core storage.
/// </summary>
[Index(nameof(TenantId))]
[Index(nameof(TenantId), nameof(PublicKey), IsUnique = true)]
public sealed class DeviceIdentityEntity
{
    [Key]
    [MaxLength(128)]
    public string DeviceId { get; set; } = null!;

    [MaxLength(128)]
    public string TenantId { get; set; } = null!;

    public bool IsPaired { get; set; }

    [MaxLength(2048)]
    public string Scopes { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(4096)]
    public string? PublicKey { get; set; }

    [MaxLength(256)]
    public string? DeviceName { get; set; }

    [MaxLength(45)]
    public string? LastIpAddress { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}
