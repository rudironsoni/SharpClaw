using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Compliance.Validation;
using SharpClaw.Tenancy.Abstractions;

namespace SharpClaw.Compliance.Rules;

/// <summary>
/// HIPAA compliance rules for healthcare data protection.
/// </summary>
public sealed class HipaaComplianceRules : IComplianceRule
{
    private readonly EncryptionValidator _encryptionValidator;
    private readonly AuditLogValidator _auditLogValidator;
    private readonly DataRetentionValidator _dataRetentionValidator;
    private readonly ComplianceEngineOptions _options;

    public string RuleId => "HIPAA-AGGREGATE";
    public string Description => "HIPAA compliance validation aggregate";
    public ComplianceStandard Standard => ComplianceStandard.HIPAA;
    public ComplianceSeverity Severity => ComplianceSeverity.Error;

    public HipaaComplianceRules(
        EncryptionValidator encryptionValidator,
        AuditLogValidator auditLogValidator,
        DataRetentionValidator dataRetentionValidator,
        IOptions<ComplianceEngineOptions> options)
    {
        _encryptionValidator = encryptionValidator;
        _auditLogValidator = auditLogValidator;
        _dataRetentionValidator = dataRetentionValidator;
        _options = options.Value;
    }

    public Task<ComplianceValidationResult> EvaluateAsync(
        ComplianceContext context,
        ITenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableHIPAA)
        {
            return Task.FromResult(ComplianceValidationResult.Compliant(RuleId));
        }

        // HIPAA only applies to PHI/PII data
        if (!context.ContainsPii && context.Classification < DataClassification.Confidential)
        {
            return Task.FromResult(ComplianceValidationResult.Compliant(RuleId));
        }

        var results = new List<ComplianceValidationResult>();

        // §164.312(a)(2)(iv) - Encryption and decryption
        var encryptRest = _encryptionValidator.ValidateEncryptionAtRest(true, "AES-256-GCM", 256);
        if (!encryptRest.IsCompliant)
        {
            results.Add(encryptRest with { RuleId = "HIPAA-164.312(a)(2)(iv)" });
        }

        var encryptTransit = _encryptionValidator.ValidateEncryptionInTransit(true, "TLS 1.3");
        if (!encryptTransit.IsCompliant)
        {
            results.Add(encryptTransit with { RuleId = "HIPAA-164.312(e)(1)" });
        }

        // §164.312(b) - Audit Controls
        var auditResult = _auditLogValidator.ValidateAuditLoggingEnabled(true);
        if (!auditResult.IsCompliant)
        {
            results.Add(auditResult with { RuleId = "HIPAA-164.312(b)" });
        }

        var piiAuditResult = _auditLogValidator.ValidatePiiAccessLogging(true, true);
        if (!piiAuditResult.IsCompliant)
        {
            results.Add(piiAuditResult with { RuleId = "HIPAA-164.312(b)-PII" });
        }

        // §164.312(c)(1) - Integrity
        var changeLogResult = _auditLogValidator.ValidateDataChangeLogging(true, true);
        if (!changeLogResult.IsCompliant)
        {
            results.Add(changeLogResult with { RuleId = "HIPAA-164.312(c)(1)" });
        }

        // §164.312(e)(1) - Transmission Security
        var piiEncrypt = _encryptionValidator.ValidatePiiEncryption(true, "AES-256-GCM", "KMS");
        if (!piiEncrypt.IsCompliant)
        {
            results.Add(piiEncrypt with { RuleId = "HIPAA-164.312(e)(1)-PII" });
        }

        // §164.530(c) - Safeguards
        var immutableResult = _auditLogValidator.ValidateAuditImmutability(true, true);
        if (!immutableResult.IsCompliant)
        {
            results.Add(immutableResult with { RuleId = "HIPAA-164.530(c)" });
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
            $"HIPAA compliance validation failed: {errorCount} errors, {warningCount} warnings",
            errorCount > 0 ? ComplianceSeverity.Error : ComplianceSeverity.Warning,
            "Review HIPAA Technical Safeguards (§164.312) and implement required controls"));
    }
}
