using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;

namespace SharpClaw.Compliance.Validation;

/// <summary>
/// Validates audit logging requirements against compliance standards.
/// </summary>
public sealed class AuditLogValidator
{
    private readonly ComplianceEngineOptions _options;

    public AuditLogValidator(IOptions<ComplianceEngineOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Validates that audit logging is enabled.
    /// </summary>
    /// <param name="isLoggingEnabled">Whether audit logging is enabled.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateAuditLoggingEnabled(bool isLoggingEnabled)
    {
        if (!_options.Audit.RequireAuditLogging)
        {
            return ComplianceValidationResult.Compliant("AUDIT-001");
        }

        if (!isLoggingEnabled)
        {
            return ComplianceValidationResult.NonCompliant(
                "AUDIT-001",
                "Audit logging is not enabled",
                ComplianceSeverity.Error,
                "Enable comprehensive audit logging for all operations");
        }

        return ComplianceValidationResult.Compliant("AUDIT-001");
    }

    /// <summary>
    /// Validates audit log immutability.
    /// </summary>
    /// <param name="isImmutable">Whether audit logs are immutable.</param>
    /// <param name="tamperDetection">Whether tamper detection is implemented.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateAuditImmutability(
        bool isImmutable,
        bool tamperDetection)
    {
        if (!_options.Audit.RequireImmutableLogs)
        {
            return ComplianceValidationResult.Compliant("AUDIT-IMMUTABLE-001");
        }

        if (!isImmutable)
        {
            return ComplianceValidationResult.NonCompliant(
                "AUDIT-IMMUTABLE-001",
                "Audit logs are not immutable",
                ComplianceSeverity.Error,
                "Implement write-once audit logs or append-only storage");
        }

        if (!tamperDetection)
        {
            return ComplianceValidationResult.NonCompliant(
                "AUDIT-IMMUTABLE-002",
                "Audit logs lack tamper detection",
                ComplianceSeverity.Warning,
                "Implement cryptographic hashing or blockchain for tamper detection");
        }

        return ComplianceValidationResult.Compliant("AUDIT-IMMUTABLE-001");
    }

    /// <summary>
    /// Validates PII access logging.
    /// </summary>
    /// <param name="accessLogged">Whether access to PII is logged.</param>
    /// <param name="includesUserContext">Whether logs include user context.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidatePiiAccessLogging(
        bool accessLogged,
        bool includesUserContext)
    {
        if (!_options.Audit.LogPiiAccess)
        {
            return ComplianceValidationResult.Compliant("AUDIT-PII-001");
        }

        if (!accessLogged)
        {
            return ComplianceValidationResult.NonCompliant(
                "AUDIT-PII-001",
                "Access to PII is not being logged",
                ComplianceSeverity.Error,
                "Log all access to personally identifiable information");
        }

        if (!includesUserContext)
        {
            return ComplianceValidationResult.NonCompliant(
                "AUDIT-PII-002",
                "PII access logs lack user context",
                ComplianceSeverity.Warning,
                "Include user identity, timestamp, and action in PII access logs");
        }

        return ComplianceValidationResult.Compliant("AUDIT-PII-001");
    }

    /// <summary>
    /// Validates data change logging.
    /// </summary>
    /// <param name="changesLogged">Whether data changes are logged.</param>
    /// <param name="includesBeforeAfter">Whether logs include before/after values.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateDataChangeLogging(
        bool changesLogged,
        bool includesBeforeAfter)
    {
        if (!_options.Audit.LogDataChanges)
        {
            return ComplianceValidationResult.Compliant("AUDIT-CHANGES-001");
        }

        if (!changesLogged)
        {
            return ComplianceValidationResult.NonCompliant(
                "AUDIT-CHANGES-001",
                "Data changes are not being logged",
                ComplianceSeverity.Error,
                "Enable audit logging for all data modifications");
        }

        if (!includesBeforeAfter)
        {
            return ComplianceValidationResult.NonCompliant(
                "AUDIT-CHANGES-002",
                "Data change logs lack before/after values",
                ComplianceSeverity.Warning,
                "Include both old and new values in change logs for forensic analysis");
        }

        return ComplianceValidationResult.Compliant("AUDIT-CHANGES-001");
    }

    /// <summary>
    /// Validates log retention period.
    /// </summary>
    /// <param name="retentionDays">The current retention period.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateLogRetention(int retentionDays)
    {
        var requiredDays = _options.DataRetention.AuditLogRetentionDays;

        if (retentionDays < requiredDays)
        {
            return ComplianceValidationResult.NonCompliant(
                "AUDIT-RETENTION-001",
                $"Audit log retention ({retentionDays} days) is less than required ({requiredDays} days)",
                ComplianceSeverity.Error,
                $"Increase audit log retention to at least {requiredDays} days");
        }

        return ComplianceValidationResult.Compliant("AUDIT-RETENTION-001");
    }
}
