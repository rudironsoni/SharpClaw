using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpClaw.Abstractions.Compliance;

/// <summary>
/// Compliance engine for enforcing regulatory requirements.
/// </summary>
public interface IComplianceEngine
{
    /// <summary>
    /// Validates an operation against compliance rules.
    /// </summary>
    Task<ComplianceResult> ValidateAsync<T>(T operation);
    
    /// <summary>
    /// Gets the current compliance configuration.
    /// </summary>
    IComplianceConfiguration Configuration { get; }
}

/// <summary>
/// Compliance configuration.
/// </summary>
public interface IComplianceConfiguration
{
    bool EnableSOC2 { get; }
    bool EnableHIPAA { get; }
    bool EnableGDPR { get; }
    bool EnableISO27001 { get; }
    AuditConfiguration Audit { get; }
    EncryptionConfiguration Encryption { get; }
}

/// <summary>
/// Audit configuration.
/// </summary>
public sealed record AuditConfiguration
{
    public bool LogAllAccess { get; init; }
    public bool LogDataChanges { get; init; }
    public int RetentionDays { get; init; }
    public bool ImmutableLogs { get; init; }
}

/// <summary>
/// Encryption configuration.
/// </summary>
public sealed record EncryptionConfiguration
{
    public bool EncryptionAtRest { get; init; }
    public bool EncryptionInTransit { get; init; }
    public string KeyManagementProvider { get; init; } = string.Empty;
}

/// <summary>
/// Compliance validation result.
/// </summary>
public sealed record ComplianceResult
{
    public bool IsCompliant => Violations.Count == 0;
    public IReadOnlyList<ComplianceViolation> Violations { get; init; } = new List<ComplianceViolation>();
}

/// <summary>
/// Compliance violation.
/// </summary>
public sealed record ComplianceViolation
{
    public required string RuleId { get; init; }
    public required string Description { get; init; }
    public string? Remediation { get; init; }
}
