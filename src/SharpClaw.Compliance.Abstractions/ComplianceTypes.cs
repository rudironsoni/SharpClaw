namespace SharpClaw.Compliance.Abstractions;

/// <summary>
/// Represents a compliance operation to be validated.
/// </summary>
/// <typeparam name="T">The type of data being operated on.</typeparam>
public sealed record ComplianceOperation<T>
{
    /// <summary>
    /// Gets the operation type identifier.
    /// </summary>
    public required string OperationType { get; init; }

    /// <summary>
    /// Gets the data being operated on.
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Gets the data classification level.
    /// </summary>
    public DataClassification Classification { get; init; } = DataClassification.Public;

    /// <summary>
    /// Gets a value indicating whether the data contains PII.
    /// </summary>
    public bool ContainsPii { get; init; }

    /// <summary>
    /// Gets the operation timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets additional metadata for the operation.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Aggregate result from the compliance engine validation.
/// </summary>
public sealed record ComplianceEngineResult
{
    /// <summary>
    /// Gets a value indicating whether all validations passed.
    /// </summary>
    public bool IsCompliant => !Results.Any(r => !r.IsCompliant && r.Severity == ComplianceSeverity.Error);

    /// <summary>
    /// Gets a value indicating whether there are any blocking errors.
    /// </summary>
    public bool HasErrors => Results.Any(r => !r.IsCompliant && r.Severity == ComplianceSeverity.Error);

    /// <summary>
    /// Gets a value indicating whether there are any warnings.
    /// </summary>
    public bool HasWarnings => Results.Any(r => !r.IsCompliant && r.Severity == ComplianceSeverity.Warning);

    /// <summary>
    /// Gets all individual validation results.
    /// </summary>
    public required IReadOnlyCollection<ComplianceValidationResult> Results { get; init; }

    /// <summary>
    /// Gets only the failed validation results.
    /// </summary>
    public IEnumerable<ComplianceValidationResult> Violations => Results.Where(r => !r.IsCompliant);

    /// <summary>
    /// Gets only the error-level violations.
    /// </summary>
    public IEnumerable<ComplianceValidationResult> Errors => Results.Where(r => !r.IsCompliant && r.Severity == ComplianceSeverity.Error);

    /// <summary>
    /// Gets only the warning-level violations.
    /// </summary>
    public IEnumerable<ComplianceValidationResult> Warnings => Results.Where(r => !r.IsCompliant && r.Severity == ComplianceSeverity.Warning);

    /// <summary>
    /// Creates a successful result with no violations.
    /// </summary>
    /// <returns>An empty compliant result.</returns>
    public static ComplianceEngineResult Success() =>
        new() { Results = Array.Empty<ComplianceValidationResult>() };

    /// <summary>
    /// Creates a result from multiple validation results.
    /// </summary>
    /// <param name="results">The individual validation results.</param>
    /// <returns>An aggregate result.</returns>
    public static ComplianceEngineResult FromResults(IEnumerable<ComplianceValidationResult> results) =>
        new() { Results = results.ToArray() };
}
