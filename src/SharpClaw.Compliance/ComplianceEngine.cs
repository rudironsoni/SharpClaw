using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Tenancy.Abstractions;
using SCA = SharpClaw.Abstractions.Compliance;
using SCCA = SharpClaw.Compliance.Abstractions;

namespace SharpClaw.Compliance;

/// <summary>
/// Tenant-aware compliance engine implementation.
/// </summary>
public sealed class ComplianceEngine : SCA.IComplianceEngine
{
    private readonly IComplianceRuleRegistry _ruleRegistry;
    private readonly ITenantComplianceProvider? _tenantProvider;
    private readonly ILogger<ComplianceEngine> _logger;
    private readonly ComplianceEngineOptions _options;

    /// <inheritdoc />
    public SCA.IComplianceConfiguration Configuration { get; }

    /// <summary>
    /// Gets all registered compliance rules.
    /// </summary>
    public IReadOnlyCollection<IComplianceRule> Rules => _ruleRegistry.GetAllRules();

    public ComplianceEngine(
        IComplianceRuleRegistry ruleRegistry,
        IOptions<ComplianceEngineOptions> options,
        ILogger<ComplianceEngine> logger,
        ITenantComplianceProvider? tenantProvider = null)
    {
        _ruleRegistry = ruleRegistry;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _options = options.Value;
        Configuration = new ComplianceConfigurationAdapter(_options);
    }

    /// <summary>
    /// Validates an operation against all enabled compliance rules.
    /// </summary>
    /// <typeparam name="T">The type of data being validated.</typeparam>
    /// <param name="operation">The operation context.</param>
    /// <param name="tenantContext">The current tenant context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregate validation result.</returns>
    public async Task<ComplianceEngineResult> ValidateAsync<T>(
        ComplianceOperation<T> operation,
        ITenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (!tenantContext.IsValid)
        {
            _logger.LogWarning("Compliance validation attempted with invalid tenant context");
            return ComplianceEngineResult.FromResults(new[]
            {
                ComplianceValidationResult.NonCompliant(
                    "TENANT-001",
                    "Invalid tenant context",
                    ComplianceSeverity.Error,
                    "Ensure tenant context is properly initialized before validation")
            });
        }

        var enabledStandards = await GetEnabledStandardsForTenantAsync(tenantContext.TenantId, cancellationToken);
        var applicableRules = GetApplicableRules(enabledStandards);

        if (applicableRules.Count == 0)
        {
            _logger.LogDebug("No compliance rules applicable for tenant {TenantId}", tenantContext.TenantId);
            return ComplianceEngineResult.Success();
        }

        _logger.LogDebug(
            "Validating {OperationType} against {RuleCount} rules for tenant {TenantId}",
            operation.OperationType,
            applicableRules.Count,
            tenantContext.TenantId);

        var context = MapToContext(operation);
        var results = new List<ComplianceValidationResult>(applicableRules.Count);

        foreach (var rule in applicableRules)
        {
            try
            {
                var result = await rule.EvaluateAsync(context, tenantContext, cancellationToken);
                results.Add(result);

                if (!result.IsCompliant)
                {
                    _logger.LogWarning(
                        "Compliance rule {RuleId} failed for tenant {TenantId}: {Message}",
                        rule.RuleId,
                        tenantContext.TenantId,
                        result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error evaluating compliance rule {RuleId} for tenant {TenantId}",
                    rule.RuleId,
                    tenantContext.TenantId);

                results.Add(ComplianceValidationResult.NonCompliant(
                    rule.RuleId,
                    $"Rule evaluation failed: {ex.Message}",
                    ComplianceSeverity.Error,
                    "Review rule implementation for errors"));
            }
        }

        var aggregateResult = ComplianceEngineResult.FromResults(results);

        LogValidationResult(operation, tenantContext, aggregateResult);

        return aggregateResult;
    }

    /// <summary>
    /// Validates an operation against a specific compliance standard.
    /// </summary>
    /// <typeparam name="T">The type of data being validated.</typeparam>
    /// <param name="operation">The operation context.</param>
    /// <param name="standard">The specific standard to validate against.</param>
    /// <param name="tenantContext">The current tenant context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result for the specified standard.</returns>
    public async Task<ComplianceEngineResult> ValidateAsync<T>(
        ComplianceOperation<T> operation,
        ComplianceStandard standard,
        ITenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (!tenantContext.IsValid)
        {
            return ComplianceEngineResult.FromResults(new[]
            {
                ComplianceValidationResult.NonCompliant(
                    "TENANT-001",
                    "Invalid tenant context",
                    ComplianceSeverity.Error)
            });
        }

        var standards = await GetEnabledStandardsForTenantAsync(tenantContext.TenantId, cancellationToken);

        if (!standards.Contains(standard))
        {
            _logger.LogDebug(
                "Standard {Standard} not enabled for tenant {TenantId}",
                standard,
                tenantContext.TenantId);
            return ComplianceEngineResult.Success();
        }

        var rules = _ruleRegistry.GetRulesForStandard(standard);
        var context = MapToContext(operation);
        var results = new List<ComplianceValidationResult>(rules.Count);

        foreach (var rule in rules)
        {
            try
            {
                var result = await rule.EvaluateAsync(context, tenantContext, cancellationToken);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error evaluating rule {RuleId} for standard {Standard}",
                    rule.RuleId,
                    standard);

                results.Add(ComplianceValidationResult.NonCompliant(
                    rule.RuleId,
                    $"Rule evaluation failed: {ex.Message}",
                    ComplianceSeverity.Error));
            }
        }

        return ComplianceEngineResult.FromResults(results);
    }

    /// <inheritdoc />
    public Task<SCA.ComplianceResult> ValidateAsync<T>(T operation)
    {
        // This is the base interface method - we need tenant context
        // Return a non-compliant result indicating tenant context is required
        var result = new SCA.ComplianceResult
        {
            Violations = new List<SCA.ComplianceViolation>
            {
                new SCA.ComplianceViolation
                {
                    RuleId = "TENANT-REQUIRED",
                    Description = "Tenant context is required for compliance validation. Use the tenant-aware overload.",
                    Remediation = "Use ComplianceEngine.ValidateAsync<T>(ComplianceOperation<T>, ITenantContext) instead"
                }
            }
        };

        return Task.FromResult(result);
    }

    private async Task<IReadOnlyCollection<ComplianceStandard>> GetEnabledStandardsForTenantAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (_tenantProvider == null)
        {
            return GetDefaultEnabledStandards();
        }

        try
        {
            return await _tenantProvider.GetEnabledStandardsAsync(tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get compliance standards for tenant {TenantId}", tenantId);
            return GetDefaultEnabledStandards();
        }
    }

    private IReadOnlyCollection<ComplianceStandard> GetDefaultEnabledStandards()
    {
        var standards = new List<ComplianceStandard>();

        if (_options.EnableSOC2)
        {
            standards.Add(ComplianceStandard.SOC2);
        }

        if (_options.EnableHIPAA)
        {
            standards.Add(ComplianceStandard.HIPAA);
        }

        if (_options.EnableGDPR)
        {
            standards.Add(ComplianceStandard.GDPR);
        }

        if (_options.EnableISO27001)
        {
            standards.Add(ComplianceStandard.ISO27001);
        }

        return standards.ToFrozenSet();
    }

    private IReadOnlyCollection<IComplianceRule> GetApplicableRules(IReadOnlyCollection<ComplianceStandard> standards)
    {
        var rules = new List<IComplianceRule>();

        foreach (var standard in standards)
        {
            rules.AddRange(_ruleRegistry.GetRulesForStandard(standard));
        }

        return rules.ToFrozenSet();
    }

    private static ComplianceContext MapToContext<T>(ComplianceOperation<T> operation)
    {
        return new ComplianceContext
        {
            OperationType = operation.OperationType,
            Data = operation.Data,
            Classification = operation.Classification,
            ContainsPii = operation.ContainsPii,
            Timestamp = operation.Timestamp,
            Metadata = operation.Metadata
        };
    }

    private void LogValidationResult<T>(
        ComplianceOperation<T> operation,
        ITenantContext tenantContext,
        ComplianceEngineResult result)
    {
        if (result.HasErrors)
        {
            _logger.LogWarning(
                "Compliance validation FAILED for {OperationType} in tenant {TenantId}. " +
                "Errors: {ErrorCount}, Warnings: {WarningCount}",
                operation.OperationType,
                tenantContext.TenantId,
                result.Errors.Count(),
                result.Warnings.Count());
        }
        else if (result.HasWarnings)
        {
            _logger.LogInformation(
                "Compliance validation PASSED with warnings for {OperationType} in tenant {TenantId}. " +
                "Warnings: {WarningCount}",
                operation.OperationType,
                tenantContext.TenantId,
                result.Warnings.Count());
        }
        else
        {
            _logger.LogDebug(
                "Compliance validation PASSED for {OperationType} in tenant {TenantId}",
                operation.OperationType,
                tenantContext.TenantId);
        }
    }

    private sealed class ComplianceConfigurationAdapter : SCA.IComplianceConfiguration
    {
        private readonly ComplianceEngineOptions _options;

        public ComplianceConfigurationAdapter(ComplianceEngineOptions options)
        {
            _options = options;
        }

        public bool EnableSOC2 => _options.EnableSOC2;
        public bool EnableHIPAA => _options.EnableHIPAA;
        public bool EnableGDPR => _options.EnableGDPR;
        public bool EnableISO27001 => _options.EnableISO27001;

        public SCA.AuditConfiguration Audit => new()
        {
            LogAllAccess = _options.Audit.RequireAuditLogging,
            LogDataChanges = _options.Audit.LogDataChanges,
            RetentionDays = _options.DataRetention.AuditLogRetentionDays,
            ImmutableLogs = _options.Audit.RequireImmutableLogs
        };

        public SCA.EncryptionConfiguration Encryption => new()
        {
            EncryptionAtRest = _options.Encryption.RequireEncryptionAtRest,
            EncryptionInTransit = _options.Encryption.RequireEncryptionInTransit,
            KeyManagementProvider = "Default"
        };
    }
}
