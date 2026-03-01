using SharpClaw.Tenancy.Abstractions;

namespace SharpClaw.Compliance.Abstractions;

/// <summary>
/// Represents a compliance rule that can be evaluated against an operation.
/// </summary>
public interface IComplianceRule
{
    /// <summary>
    /// Gets the unique identifier of the rule.
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// Gets the human-readable description of the rule.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the compliance standard this rule belongs to.
    /// </summary>
    ComplianceStandard Standard { get; }

    /// <summary>
    /// Gets the severity level if this rule is violated.
    /// </summary>
    ComplianceSeverity Severity { get; }

    /// <summary>
    /// Evaluates the rule against the provided context.
    /// </summary>
    /// <param name="context">The compliance context containing operation details.</param>
    /// <param name="tenantContext">The current tenant context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result indicating compliance status.</returns>
    Task<ComplianceValidationResult> EvaluateAsync(
        ComplianceContext context,
        ITenantContext tenantContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the context for a compliance evaluation.
/// </summary>
public sealed record ComplianceContext
{
    /// <summary>
    /// Gets the operation type being validated.
    /// </summary>
    public required string OperationType { get; init; }

    /// <summary>
    /// Gets the data being operated on.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Gets the data classification level.
    /// </summary>
    public DataClassification Classification { get; init; } = DataClassification.Public;

    /// <summary>
    /// Gets a value indicating whether the operation involves PII.
    /// </summary>
    public bool ContainsPii { get; init; }

    /// <summary>
    /// Gets the timestamp of the operation.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets additional metadata for the operation.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Result of a compliance rule evaluation.
/// </summary>
public sealed record ComplianceValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the rule passed validation.
    /// </summary>
    public required bool IsCompliant { get; init; }

    /// <summary>
    /// Gets the rule identifier that produced this result.
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// Gets the human-readable message explaining the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the severity level of any violation.
    /// </summary>
    public ComplianceSeverity Severity { get; init; } = ComplianceSeverity.Info;

    /// <summary>
    /// Gets suggested remediation steps if non-compliant.
    /// </summary>
    public string? Remediation { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="ruleId">The rule identifier.</param>
    /// <returns>A compliant validation result.</returns>
    public static ComplianceValidationResult Compliant(string ruleId) =>
        new() { IsCompliant = true, RuleId = ruleId };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="ruleId">The rule identifier.</param>
    /// <param name="message">The violation message.</param>
    /// <param name="severity">The severity level.</param>
    /// <param name="remediation">Optional remediation steps.</param>
    /// <returns>A non-compliant validation result.</returns>
    public static ComplianceValidationResult NonCompliant(
        string ruleId,
        string message,
        ComplianceSeverity severity = ComplianceSeverity.Error,
        string? remediation = null) =>
        new()
        {
            IsCompliant = false,
            RuleId = ruleId,
            Message = message,
            Severity = severity,
            Remediation = remediation
        };
}
