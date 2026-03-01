using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;

namespace SharpClaw.Compliance.Validation;

/// <summary>
/// Validates data retention policies against compliance requirements.
/// </summary>
public sealed class DataRetentionValidator
{
    private readonly ComplianceEngineOptions _options;

    public DataRetentionValidator(IOptions<ComplianceEngineOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Validates data retention for the given classification.
    /// </summary>
    /// <param name="dataClassification">The data classification.</param>
    /// <param name="retentionDays">The actual retention period.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateRetention(
        DataClassification dataClassification,
        int retentionDays)
    {
        var requiredRetention = GetRequiredRetentionDays(dataClassification);

        if (retentionDays < requiredRetention)
        {
            return ComplianceValidationResult.NonCompliant(
                "DATA-RETENTION-001",
                $"Data retention period ({retentionDays} days) is less than required minimum ({requiredRetention} days) " +
                $"for classification {dataClassification}",
                ComplianceSeverity.Error,
                $"Increase retention period to at least {requiredRetention} days");
        }

        return ComplianceValidationResult.Compliant("DATA-RETENTION-001");
    }

    /// <summary>
    /// Validates audit log retention.
    /// </summary>
    /// <param name="retentionDays">The current audit log retention.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateAuditLogRetention(int retentionDays)
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

    /// <summary>
    /// Validates PII data retention.
    /// </summary>
    /// <param name="retentionDays">The current PII retention period.</param>
    /// <param name="hasConsent">Whether user consent is documented.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidatePiiRetention(int retentionDays, bool hasConsent)
    {
        if (!hasConsent)
        {
            return ComplianceValidationResult.NonCompliant(
                "PII-RETENTION-001",
                "PII data retention without documented user consent",
                ComplianceSeverity.Error,
                "Obtain and document user consent for PII retention");
        }

        var requiredDays = _options.DataRetention.PiiRetentionDays;

        if (retentionDays > requiredDays)
        {
            return ComplianceValidationResult.NonCompliant(
                "PII-RETENTION-002",
                $"PII retention period ({retentionDays} days) exceeds maximum allowed ({requiredDays} days)",
                ComplianceSeverity.Warning,
                $"Reduce PII retention to {requiredDays} days or implement data purging");
        }

        return ComplianceValidationResult.Compliant("PII-RETENTION-001");
    }

    private int GetRequiredRetentionDays(DataClassification classification)
    {
        return classification switch
        {
            DataClassification.Public => 30,
            DataClassification.Internal => 90,
            DataClassification.Confidential => _options.DataRetention.DefaultRetentionDays,
            DataClassification.Restricted => _options.DataRetention.DefaultRetentionDays * 2,
            _ => _options.DataRetention.DefaultRetentionDays
        };
    }
}
