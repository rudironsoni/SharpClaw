namespace SharpClaw.Compliance.Abstractions;

/// <summary>
/// Defines compliance standards supported by the engine.
/// </summary>
public enum ComplianceStandard
{
    /// <summary>
    /// SOC 2 Type II compliance standard.
    /// </summary>
    SOC2,

    /// <summary>
    /// HIPAA compliance for healthcare data.
    /// </summary>
    HIPAA,

    /// <summary>
    /// GDPR compliance for EU data protection.
    /// </summary>
    GDPR,

    /// <summary>
    /// ISO 27001 information security standard.
    /// </summary>
    ISO27001
}

/// <summary>
/// Defines the severity level of a compliance rule violation.
/// </summary>
public enum ComplianceSeverity
{
    /// <summary>
    /// Informational message, no action required.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that should be addressed but doesn't block operation.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that must be fixed before operation can proceed.
    /// </summary>
    Error
}

/// <summary>
/// Represents the type of data classification.
/// </summary>
public enum DataClassification
{
    /// <summary>
    /// Public data that can be freely shared.
    /// </summary>
    Public,

    /// <summary>
    /// Internal data for organization use only.
    /// </summary>
    Internal,

    /// <summary>
    /// Confidential data with restricted access.
    /// </summary>
    Confidential,

    /// <summary>
    /// Highly sensitive data requiring maximum protection.
    /// </summary>
    Restricted
}
