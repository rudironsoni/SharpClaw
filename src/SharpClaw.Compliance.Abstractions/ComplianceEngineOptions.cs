namespace SharpClaw.Compliance.Abstractions;

/// <summary>
/// Configuration options for the compliance engine.
/// </summary>
public sealed class ComplianceEngineOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether SOC 2 compliance is enabled.
    /// </summary>
    public bool EnableSOC2 { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether HIPAA compliance is enabled.
    /// </summary>
    public bool EnableHIPAA { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether GDPR compliance is enabled.
    /// </summary>
    public bool EnableGDPR { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether ISO 27001 compliance is enabled.
    /// </summary>
    public bool EnableISO27001 { get; set; } = true;

    /// <summary>
    /// Gets the data retention configuration.
    /// </summary>
    public DataRetentionOptions DataRetention { get; } = new();

    /// <summary>
    /// Gets the encryption configuration.
    /// </summary>
    public EncryptionOptions Encryption { get; } = new();

    /// <summary>
    /// Gets the audit logging configuration.
    /// </summary>
    public AuditOptions Audit { get; } = new();

    /// <summary>
    /// Gets the access control configuration.
    /// </summary>
    public AccessControlOptions AccessControl { get; } = new();
}

/// <summary>
/// Data retention policy configuration.
/// </summary>
public sealed class DataRetentionOptions
{
    /// <summary>
    /// Gets or sets the default retention period in days.
    /// </summary>
    public int DefaultRetentionDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets the retention period for PII data in days.
    /// </summary>
    public int PiiRetentionDays { get; set; } = 2555; // 7 years

    /// <summary>
    /// Gets or sets the retention period for audit logs in days.
    /// </summary>
    public int AuditLogRetentionDays { get; set; } = 2555; // 7 years

    /// <summary>
    /// Gets or sets a value indicating whether automatic purging is enabled.
    /// </summary>
    public bool EnableAutomaticPurging { get; set; } = true;
}

/// <summary>
/// Encryption requirement configuration.
/// </summary>
public sealed class EncryptionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether encryption at rest is required.
    /// </summary>
    public bool RequireEncryptionAtRest { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether encryption in transit is required.
    /// </summary>
    public bool RequireEncryptionInTransit { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum encryption key length in bits.
    /// </summary>
    public int MinimumKeyLength { get; set; } = 256;

    /// <summary>
    /// Gets or sets the allowed encryption algorithms.
    /// </summary>
    public IReadOnlyList<string> AllowedAlgorithms { get; set; } = new[] { "AES-256-GCM", "AES-256-CBC", "RSA-4096" };
}

/// <summary>
/// Audit logging configuration.
/// </summary>
public sealed class AuditOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether all access must be logged.
    /// </summary>
    public bool RequireAuditLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether data modifications must be logged.
    /// </summary>
    public bool LogDataChanges { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether access to PII must be logged.
    /// </summary>
    public bool LogPiiAccess { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether audit logs must be immutable.
    /// </summary>
    public bool RequireImmutableLogs { get; set; } = true;
}

/// <summary>
/// Access control configuration.
/// </summary>
public sealed class AccessControlOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether role-based access control is required.
    /// </summary>
    public bool RequireRBAC { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether principle of least privilege is enforced.
    /// </summary>
    public bool EnforceLeastPrivilege { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether regular access reviews are required.
    /// </summary>
    public bool RequireAccessReviews { get; set; } = true;
}
