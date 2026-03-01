using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SharpClaw.Persistence.Contracts.Entities;

/// <summary>
/// Configuration revision entity for EF Core storage.
/// </summary>
[Index(nameof(TenantId))]
[Index(nameof(TenantId), nameof(IsActive))]
[Index(nameof(CreatedAt))]
public sealed class ConfigRevisionEntity
{
    [Key]
    [MaxLength(128)]
    public string RevisionId { get; set; } = null!;

    [MaxLength(128)]
    public string TenantId { get; set; } = null!;

    [MaxLength(64)]
    public string Hash { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(128)]
    public string? CreatedBy { get; set; }

    public string? Configuration { get; set; }

    [MaxLength(512)]
    public string? ChangeDescription { get; set; }
}
