using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Compliance.Validation;
using SharpClaw.Tenancy.Abstractions;

namespace SharpClaw.Compliance.Rules;

/// <summary>
/// SOC 2 Type II compliance rules implementation.
/// </summary>
public sealed class Soc2ComplianceRules : IComplianceRule
{
    private readonly EncryptionValidator _encryptionValidator;
    private readonly AuditLogValidator _auditLogValidator;
    private readonly AccessControlValidator _accessControlValidator;
    private readonly ComplianceEngineOptions _options;

    public string RuleId => "SOC2-AGGREGATE";
    public string Description => "SOC 2 Type II compliance validation aggregate";
    public ComplianceStandard Standard => ComplianceStandard.SOC2;
    public ComplianceSeverity Severity => ComplianceSeverity.Error;

    public Soc2ComplianceRules(
        EncryptionValidator encryptionValidator,
        AuditLogValidator auditLogValidator,
        AccessControlValidator accessControlValidator,
        IOptions<ComplianceEngineOptions> options)
    {
        _encryptionValidator = encryptionValidator;
        _auditLogValidator = auditLogValidator;
        _accessControlValidator = accessControlValidator;
        _options = options.Value;
    }

    public async Task<ComplianceValidationResult> EvaluateAsync(
        ComplianceContext context,
        ITenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableSOC2)
        {
            return ComplianceValidationResult.Compliant(RuleId);
        }

        var results = new List<ComplianceValidationResult>();

        // CC6.1 - Logical access controls
        var rbacResult = _accessControlValidator.ValidateRBAC(true, true);
        if (!rbacResult.IsCompliant)
        {
            results.Add(rbacResult with { RuleId = "SOC2-CC6.1" });
        }

        // CC6.2 - Access removal
        var leastPrivResult = _accessControlValidator.ValidateLeastPrivilege(true, true);
        if (!leastPrivResult.IsCompliant)
        {
            results.Add(leastPrivResult with { RuleId = "SOC2-CC6.2" });
        }

        // CC6.3 - Access reviews
        var reviewResult = _accessControlValidator.ValidateAccessReviews(true, 90);
        if (!reviewResult.IsCompliant)
        {
            results.Add(reviewResult with { RuleId = "SOC2-CC6.3" });
        }

        // CC6.6 - Encryption
        if (context.Classification >= DataClassification.Confidential)
        {
            var encryptResult = _encryptionValidator.ValidateEncryptionAtRest(true, "AES-256-GCM", 256);
            if (!encryptResult.IsCompliant)
            {
                results.Add(encryptResult with { RuleId = "SOC2-CC6.6" });
            }
        }

        // CC7.2 - System monitoring
        var auditResult = _auditLogValidator.ValidateAuditLoggingEnabled(true);
        if (!auditResult.IsCompliant)
        {
            results.Add(auditResult with { RuleId = "SOC2-CC7.2" });
        }

        // CC7.3 - Log retention
        var retentionResult = _auditLogValidator.ValidateLogRetention(_options.DataRetention.AuditLogRetentionDays);
        if (!retentionResult.IsCompliant)
        {
            results.Add(retentionResult with { RuleId = "SOC2-CC7.3" });
        }

        // Aggregate results
        if (results.Count == 0)
        {
            return ComplianceValidationResult.Compliant(RuleId);
        }

        var errorCount = results.Count(r => r.Severity == ComplianceSeverity.Error);
        var warningCount = results.Count(r => r.Severity == ComplianceSeverity.Warning);

        return ComplianceValidationResult.NonCompliant(
            RuleId,
            $"SOC 2 compliance validation failed: {errorCount} errors, {warningCount} warnings",
            errorCount > 0 ? ComplianceSeverity.Error : ComplianceSeverity.Warning,
            "Review individual control failures and implement required controls");
    }
}
