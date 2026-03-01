using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Compliance.Validation;
using SharpClaw.Tenancy.Abstractions;

namespace SharpClaw.Compliance.Rules;

/// <summary>
/// GDPR compliance rules for EU data protection.
/// </summary>
public sealed class GdprComplianceRules : IComplianceRule
{
    private readonly DataRetentionValidator _dataRetentionValidator;
    private readonly EncryptionValidator _encryptionValidator;
    private readonly AuditLogValidator _auditLogValidator;
    private readonly ComplianceEngineOptions _options;

    public string RuleId => "GDPR-AGGREGATE";
    public string Description => "GDPR compliance validation aggregate";
    public ComplianceStandard Standard => ComplianceStandard.GDPR;
    public ComplianceSeverity Severity => ComplianceSeverity.Error;

    public GdprComplianceRules(
        DataRetentionValidator dataRetentionValidator,
        EncryptionValidator encryptionValidator,
        AuditLogValidator auditLogValidator,
        IOptions<ComplianceEngineOptions> options)
    {
        _dataRetentionValidator = dataRetentionValidator;
        _encryptionValidator = encryptionValidator;
        _auditLogValidator = auditLogValidator;
        _options = options.Value;
    }

    public Task<ComplianceValidationResult> EvaluateAsync(
        ComplianceContext context,
        ITenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableGDPR)
        {
            return Task.FromResult(ComplianceValidationResult.Compliant(RuleId));
        }

        // GDPR primarily applies to PII
        if (!context.ContainsPii)
        {
            return Task.FromResult(ComplianceValidationResult.Compliant(RuleId));
        }

        var results = new List<ComplianceValidationResult>();

        // Article 5(1)(e) - Storage limitation
        var hasConsent = context.Metadata.TryGetValue("gdpr_consent", out var consentObj) &&
                        consentObj is bool consent && consent;

        var retentionResult = _dataRetentionValidator.ValidatePiiRetention(
            _options.DataRetention.PiiRetentionDays,
            hasConsent);
        if (!retentionResult.IsCompliant)
        {
            results.Add(retentionResult with { RuleId = "GDPR-Art5(1)(e)" });
        }

        // Article 32(1)(a) - Pseudonymisation and encryption
        var encryptRest = _encryptionValidator.ValidatePiiEncryption(true, "AES-256-GCM", "KMS");
        if (!encryptRest.IsCompliant)
        {
            results.Add(encryptRest with { RuleId = "GDPR-Art32(1)(a)" });
        }

        // Article 32(1)(b) - Ongoing confidentiality
        var transitResult = _encryptionValidator.ValidateEncryptionInTransit(true, "TLS 1.3");
        if (!transitResult.IsCompliant)
        {
            results.Add(transitResult with { RuleId = "GDPR-Art32(1)(b)" });
        }

        // Article 33 - Breach notification requires audit logs
        var auditResult = _auditLogValidator.ValidateAuditLoggingEnabled(true);
        if (!auditResult.IsCompliant)
        {
            results.Add(auditResult with { RuleId = "GDPR-Art33" });
        }

        // Article 30 - Records of processing
        var changeLogResult = _auditLogValidator.ValidateDataChangeLogging(true, true);
        if (!changeLogResult.IsCompliant)
        {
            results.Add(changeLogResult with { RuleId = "GDPR-Art30" });
        }

        // Article 5(1)(f) - Integrity and confidentiality
        var immutableResult = _auditLogValidator.ValidateAuditImmutability(true, true);
        if (!immutableResult.IsCompliant)
        {
            results.Add(immutableResult with { RuleId = "GDPR-Art5(1)(f)" });
        }

        // Article 25 - Data protection by design
        if (context.Classification >= DataClassification.Confidential && !hasConsent)
        {
            results.Add(ComplianceValidationResult.NonCompliant(
                "GDPR-Art25",
                "Processing of confidential data without consent violates data protection by design",
                ComplianceSeverity.Error,
                "Implement consent management and privacy-by-design principles"));
        }

        // Aggregate results
        if (results.Count == 0)
        {
            return Task.FromResult(ComplianceValidationResult.Compliant(RuleId));
        }

        var errorCount = results.Count(r => r.Severity == ComplianceSeverity.Error);
        var warningCount = results.Count(r => r.Severity == ComplianceSeverity.Warning);

        return Task.FromResult(ComplianceValidationResult.NonCompliant(
            RuleId,
            $"GDPR compliance validation failed: {errorCount} errors, {warningCount} warnings",
            errorCount > 0 ? ComplianceSeverity.Error : ComplianceSeverity.Warning,
            "Review GDPR Articles 5, 25, 30, 32 and implement required controls"));
    }
}
