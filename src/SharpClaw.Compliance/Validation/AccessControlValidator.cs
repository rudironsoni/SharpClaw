using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;

namespace SharpClaw.Compliance.Validation;

/// <summary>
/// Validates access control requirements against compliance standards.
/// </summary>
public sealed class AccessControlValidator
{
    private readonly ComplianceEngineOptions _options;

    public AccessControlValidator(IOptions<ComplianceEngineOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Validates role-based access control implementation.
    /// </summary>
    /// <param name="hasRBAC">Whether RBAC is implemented.</param>
    /// <param name="rolesDefined">Whether roles are properly defined.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateRBAC(
        bool hasRBAC,
        bool rolesDefined)
    {
        if (!_options.AccessControl.RequireRBAC)
        {
            return ComplianceValidationResult.Compliant("AC-RBAC-001");
        }

        if (!hasRBAC)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-RBAC-001",
                "Role-based access control is not implemented",
                ComplianceSeverity.Error,
                "Implement RBAC with clearly defined roles and permissions");
        }

        if (!rolesDefined)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-RBAC-002",
                "RBAC roles are not properly defined",
                ComplianceSeverity.Warning,
                "Document all roles, their permissions, and approval workflows");
        }

        return ComplianceValidationResult.Compliant("AC-RBAC-001");
    }

    /// <summary>
    /// Validates principle of least privilege.
    /// </summary>
    /// <param name="followsLeastPrivilege">Whether least privilege is followed.</param>
    /// <param name="regularReviews">Whether access is regularly reviewed.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateLeastPrivilege(
        bool followsLeastPrivilege,
        bool regularReviews)
    {
        if (!_options.AccessControl.EnforceLeastPrivilege)
        {
            return ComplianceValidationResult.Compliant("AC-LEAST-PRIV-001");
        }

        if (!followsLeastPrivilege)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-LEAST-PRIV-001",
                "Principle of least privilege is not enforced",
                ComplianceSeverity.Error,
                "Grant users only the minimum permissions required for their role");
        }

        if (!regularReviews)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-LEAST-PRIV-002",
                "Access permissions are not regularly reviewed",
                ComplianceSeverity.Warning,
                "Implement quarterly access reviews to ensure continued adherence to least privilege");
        }

        return ComplianceValidationResult.Compliant("AC-LEAST-PRIV-001");
    }

    /// <summary>
    /// Validates access control for sensitive data.
    /// </summary>
    /// <param name="classification">The data classification.</param>
    /// <param name="hasAccessControl">Whether access control is implemented.</param>
    /// <param name="requiresApproval">Whether access requires approval.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateSensitiveDataAccess(
        DataClassification classification,
        bool hasAccessControl,
        bool requiresApproval)
    {
        if (classification == DataClassification.Public)
        {
            return ComplianceValidationResult.Compliant("AC-SENSITIVE-001");
        }

        if (!hasAccessControl)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-SENSITIVE-001",
                $"Access control not implemented for {classification} data",
                ComplianceSeverity.Error,
                "Implement access control for all non-public data");
        }

        if (classification == DataClassification.Restricted && !requiresApproval)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-SENSITIVE-002",
                "Restricted data access does not require approval",
                ComplianceSeverity.Error,
                "Implement approval workflow for accessing restricted data");
        }

        return ComplianceValidationResult.Compliant("AC-SENSITIVE-001");
    }

    /// <summary>
    /// Validates multi-factor authentication requirements.
    /// </summary>
    /// <param name="mfaEnabled">Whether MFA is enabled.</param>
    /// <param name="mfaRequiredForSensitive">Whether MFA is required for sensitive access.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateMFA(
        bool mfaEnabled,
        bool mfaRequiredForSensitive)
    {
        if (!mfaEnabled)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-MFA-001",
                "Multi-factor authentication is not enabled",
                ComplianceSeverity.Warning,
                "Enable MFA for all user accounts, especially for administrative access");
        }

        if (!mfaRequiredForSensitive)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-MFA-002",
                "MFA is not required for accessing sensitive data",
                ComplianceSeverity.Warning,
                "Require MFA for access to confidential and restricted data");
        }

        return ComplianceValidationResult.Compliant("AC-MFA-001");
    }

    /// <summary>
    /// Validates access review process.
    /// </summary>
    /// <param name="reviewsConducted">Whether reviews are conducted.</param>
    /// <param name="reviewFrequency">Review frequency in days.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateAccessReviews(
        bool reviewsConducted,
        int reviewFrequency)
    {
        if (!_options.AccessControl.RequireAccessReviews)
        {
            return ComplianceValidationResult.Compliant("AC-REVIEWS-001");
        }

        if (!reviewsConducted)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-REVIEWS-001",
                "Access reviews are not being conducted",
                ComplianceSeverity.Error,
                "Implement regular access reviews to ensure continued need for permissions");
        }

        if (reviewFrequency > 90)
        {
            return ComplianceValidationResult.NonCompliant(
                "AC-REVIEWS-002",
                $"Access review frequency ({reviewFrequency} days) exceeds recommended maximum (90 days)",
                ComplianceSeverity.Warning,
                "Conduct access reviews at least quarterly (every 90 days)");
        }

        return ComplianceValidationResult.Compliant("AC-REVIEWS-001");
    }
}
