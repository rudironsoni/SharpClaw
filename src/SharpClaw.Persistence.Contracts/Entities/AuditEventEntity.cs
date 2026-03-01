using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SharpClaw.Persistence.Contracts.Entities;

/// <summary>
/// Audit event entity for EF Core storage.
/// </summary>
[Index(nameof(TenantId))]
[Index(nameof(Timestamp))]
[Index(nameof(EntityType))]
[Index(nameof(Action))]
[Index(nameof(UserId))]
public sealed class AuditEventEntity
{
    [Key]
    [MaxLength(128)]
    public string EventId { get; set; } = null!;

    [MaxLength(128)]
    public string TenantId { get; set; } = null!;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(128)]
    public string EntityType { get; set; } = null!;

    [MaxLength(128)]
    public string EntityId { get; set; } = null!;

    [MaxLength(50)]
    public string Action { get; set; } = null!; // Created, Updated, Deleted, Accessed

    [MaxLength(128)]
    public string? UserId { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    [MaxLength(2048)]
    public string? Metadata { get; set; }

    public bool IsSensitive { get; set; }
}
