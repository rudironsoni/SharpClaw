namespace SharpClaw.Compliance.Abstractions;

/// <summary>
/// Registry for managing compliance rules.
/// </summary>
public interface IComplianceRuleRegistry
{
    /// <summary>
    /// Registers a compliance rule.
    /// </summary>
    /// <param name="rule">The rule to register.</param>
    void RegisterRule(IComplianceRule rule);

    /// <summary>
    /// Unregisters a compliance rule by ID.
    /// </summary>
    /// <param name="ruleId">The rule identifier.</param>
    /// <returns>True if the rule was found and removed.</returns>
    bool UnregisterRule(string ruleId);

    /// <summary>
    /// Gets all registered rules.
    /// </summary>
    /// <returns>All registered compliance rules.</returns>
    IReadOnlyCollection<IComplianceRule> GetAllRules();

    /// <summary>
    /// Gets rules for a specific compliance standard.
    /// </summary>
    /// <param name="standard">The compliance standard.</param>
    /// <returns>Rules applicable to the standard.</returns>
    IReadOnlyCollection<IComplianceRule> GetRulesForStandard(ComplianceStandard standard);

    /// <summary>
    /// Gets a rule by its ID.
    /// </summary>
    /// <param name="ruleId">The rule identifier.</param>
    /// <returns>The rule if found, null otherwise.</returns>
    IComplianceRule? GetRuleById(string ruleId);
}

/// <summary>
/// Provider for tenant-specific compliance configurations.
/// </summary>
public interface ITenantComplianceProvider
{
    /// <summary>
    /// Gets the compliance configuration for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant's compliance configuration.</returns>
    Task<TenantComplianceConfig> GetConfigurationAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the enabled compliance standards for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The enabled standards for the tenant.</returns>
    Task<IReadOnlyCollection<ComplianceStandard>> GetEnabledStandardsAsync(string tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant-specific compliance configuration.
/// </summary>
public sealed record TenantComplianceConfig
{
    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the enabled compliance standards.
    /// </summary>
    public required IReadOnlyCollection<ComplianceStandard> EnabledStandards { get; init; }

    /// <summary>
    /// Gets custom rule configurations for this tenant.
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomRules { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the data retention override in days, if set.
    /// </summary>
    public int? DataRetentionDays { get; init; }

    /// <summary>
    /// Gets a value indicating whether this tenant requires enhanced audit logging.
    /// </summary>
    public bool EnhancedAuditLogging { get; init; }
}
