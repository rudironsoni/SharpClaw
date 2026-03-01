using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SharpClaw.Persistence.Contracts.Entities;

/// <summary>
/// Run record entity for EF Core storage.
/// </summary>
[Index(nameof(TenantId))]
[Index(nameof(TenantId), nameof(IdempotencyKey), IsUnique = true)]
[Index(nameof(Status))]
[Index(nameof(CreatedAt))]
public sealed class RunRecordEntity
{
    [Key]
    [MaxLength(128)]
    public string RunId { get; set; } = null!;

    [MaxLength(128)]
    public string TenantId { get; set; } = null!;

    [MaxLength(128)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    [MaxLength(50)]
    public string? Provider { get; set; }

    [MaxLength(256)]
    public string? SandboxId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    [MaxLength(128)]
    public string? DeviceId { get; set; }

    public string? InputData { get; set; }

    public string? OutputData { get; set; }

    public string? ErrorData { get; set; }

    public long? InputTokens { get; set; }

    public long? OutputTokens { get; set; }

    public int RetryCount { get; set; }

    [MaxLength(2048)]
    public string? Metadata { get; set; }
}
