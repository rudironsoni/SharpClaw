using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SharpClaw.Persistence.Contracts.Entities;

/// <summary>
/// Idempotency key entity for EF Core storage.
/// </summary>
[Index(nameof(TenantId))]
[Index(nameof(TenantId), nameof(Key), IsUnique = true)]
[Index(nameof(ExpiresAt))]
public sealed class IdempotencyKeyEntity
{
    [Key]
    [MaxLength(128)]
    public string IdempotencyKeyId { get; set; } = null!;

    [MaxLength(128)]
    public string TenantId { get; set; } = null!;

    [MaxLength(512)]
    public string Key { get; set; } = null!;

    [MaxLength(128)]
    public string? RunId { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public string? RequestPayload { get; set; }

    public string? ResponsePayload { get; set; }
}
