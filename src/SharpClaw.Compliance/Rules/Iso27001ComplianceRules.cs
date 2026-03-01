using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Compliance.Validation;
using SharpClaw.Tenancy.Abstractions;

namespace SharpClaw.Compliance.Rules;

/// <summary>
/// ISO 27001 compliance rules for information security management.
/// </summary>
public sealed class Iso27001ComplianceRules : IComplianceRule
{
    private readonly EncryptionValidator _encryptionValidator;
    private readonly AccessControlValidator _accessControlValidator;
    private readonly AuditLogValidator _auditLogValidator;
    private readonly ComplianceEngineOptions _options;

    public string RuleId => "ISO27001-AGGREGATE";
    public string Description => "ISO 27001 compliance validation aggregate";
    public ComplianceStandard Standard => ComplianceStandard.ISO27001;
    public ComplianceSeverity Severity => ComplianceSeverity.Error;

    public Iso27001ComplianceRules(
        EncryptionValidator encryptionValidator,
        AccessControlValidator accessControlValidator,
        AuditLogValidator auditLogValidator,
        IOptions<ComplianceEngineOptions> options)
    {
        _encryptionValidator = encryptionValidator;
        _accessControlValidator = accessControlValidator;
        _auditLogValidator = auditLogValidator;
        _options = options.Value;
    }

    public Task<ComplianceValidationResult> EvaluateAsync(
        ComplianceContext context,
        ITenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableISO27001)
        {
            return Task.FromResult(ComplianceValidationResult.Compliant(RuleId));
        }

        var results = new List<ComplianceValidationResult>();

        // A.5.15 - Access control policy
        var rbacResult = _accessControlValidator.ValidateRBAC(true, true);
        if (!rbacResult.IsCompliant)
        {
            results.Add(rbacResult with { RuleId = "ISO27001-A.5.15" });
        }

        // A.5.18 - Access rights
        var leastPrivResult = _accessControlValidator.ValidateLeastPrivilege(true, true);
        if (!leastPrivResult.IsCompliant)
        {
            results.Add(leastPrivResult with { RuleId = "ISO27001-A.5.18" });
        }

        // A.5.22 - Regular review of access rights
        var reviewResult = _accessControlValidator.ValidateAccessReviews(true, 90);
        if (!reviewResult.IsCompliant)
        {
            results.Add(reviewResult with { RuleId = "ISO27001-A.5.22" });
        }

        // A.8.5 - Secure authentication
        var mfaResult = _accessControlValidator.ValidateMFA(true, true);
        if (!mfaResult.IsCompliant)
        {
            results.Add(mfaResult with { RuleId = "ISO27001-A.8.5" });
        }

        // A.8.24 - Use of cryptography
        if (context.Classification >= DataClassification.Internal)
        {
            var encryptRest = _encryptionValidator.ValidateEncryptionAtRest(true, "AES-256-GCM", 256);
            if (!encryptRest.IsCompliant)
            {
                results.Add(encryptRest with { RuleId = "ISO27001-A.8.24" });
            }
        }

        // A.8.25 - Protection of data in transit
        var encryptTransit = _encryptionValidator.ValidateEncryptionInTransit(true, "TLS 1.3");
        if (!encryptTransit.IsCompliant)
        {
            results.Add(encryptTransit with { RuleId = "ISO27001-A.8.25" });
        }

        // A.8.26 - Information security aspects of business continuity
        var immutableResult = _auditLogValidator.ValidateAuditImmutability(true, true);
        if (!immutableResult.IsCompliant)
        {
            results.Add(immutableResult with { RuleId = "ISO27001-A.8.26" });
        }

        // A.8.11 - Data masking
        if (context.ContainsPii)
        {
            var piiEncrypt = _encryptionValidator.ValidatePiiEncryption(true, "AES-256-GCM", "KMS");
            if (!piiEncrypt.IsCompliant)
            {
                results.Add(piiEncrypt with { RuleId = "ISO27001-A.8.11" });
            }
        }

        // A.8.15 - Logging
        var auditResult = _auditLogValidator.ValidateAuditLoggingEnabled(true);
        if (!auditResult.IsCompliant)
        {
            results.Add(auditResult with { RuleId = "ISO27001-A.8.15" });
        }

        // A.8.16 - Monitoring activities
        var changeLogResult = _auditLogValidator.ValidateDataChangeLogging(true, true);
        if (!changeLogResult.IsCompliant)
        {
            results.Add(changeLogResult with { RuleId = "ISO27001-A.8.16" });
        }

        // A.8.17 - Protection of log information
        var retentionResult = _auditLogValidator.ValidateLogRetention(_options.DataRetention.AuditLogRetentionDays);
        if (!retentionResult.IsCompliant)
        {
            results.Add(retentionResult with { RuleId = "ISO27001-A.8.17" });
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
            $"ISO 27001 compliance validation failed: {errorCount} errors, {warningCount} warnings",
            errorCount > 0 ? ComplianceSeverity.Error : ComplianceSeverity.Warning,
            "Review ISO 27001 Annex A controls and implement required security measures"));
    }
}
