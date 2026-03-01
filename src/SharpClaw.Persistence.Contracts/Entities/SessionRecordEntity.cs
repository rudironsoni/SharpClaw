using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SharpClaw.Persistence.Contracts.Entities;

/// <summary>
/// Session record entity for EF Core storage.
/// </summary>
[Index(nameof(TenantId))]
[Index(nameof(TenantId), nameof(DeviceId))]
[Index(nameof(ExpiresAt))]
public sealed class SessionRecordEntity
{
    [Key]
    [MaxLength(128)]
    public string SessionId { get; set; } = null!;

    [MaxLength(128)]
    public string TenantId { get; set; } = null!;

    [MaxLength(128)]
    public string DeviceId { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? LastActivityAt { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    public bool IsActive { get; set; } = true;
}
