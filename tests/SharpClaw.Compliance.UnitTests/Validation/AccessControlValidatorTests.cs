using FluentAssertions;
using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Compliance.Validation;

namespace SharpClaw.Compliance.UnitTests.Validation;

public sealed class AccessControlValidatorTests
{
    private readonly AccessControlValidator _validator;
    private readonly ComplianceEngineOptions _options;

    public AccessControlValidatorTests()
    {
        _options = new ComplianceEngineOptions();
        _options.AccessControl.RequireRBAC = true;
        _options.AccessControl.EnforceLeastPrivilege = true;
        _options.AccessControl.RequireAccessReviews = true;

        var optionsWrapper = Options.Create(_options);
        _validator = new AccessControlValidator(optionsWrapper);
    }

    [Fact]
    public void ValidateRBAC_ImplementedWithDefinedRoles_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateRBAC(true, true);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateRBAC_NotImplemented_ReturnsError()
    {
        // Act
        var result = _validator.ValidateRBAC(false, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AC-RBAC-001");
    }

    [Fact]
    public void ValidateRBAC_RolesNotDefined_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidateRBAC(true, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("AC-RBAC-002");
    }

    [Fact]
    public void ValidateRBAC_DisabledInOptions_ReturnsCompliant()
    {
        // Arrange
        _options.AccessControl.RequireRBAC = false;

        // Act
        var result = _validator.ValidateRBAC(false, false);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateLeastPrivilege_EnforcedWithReviews_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateLeastPrivilege(true, true);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateLeastPrivilege_NotEnforced_ReturnsError()
    {
        // Act
        var result = _validator.ValidateLeastPrivilege(false, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AC-LEAST-PRIV-001");
    }

    [Fact]
    public void ValidateLeastPrivilege_NoRegularReviews_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidateLeastPrivilege(true, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("AC-LEAST-PRIV-002");
    }

    [Fact]
    public void ValidateSensitiveDataAccess_PublicData_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateSensitiveDataAccess(
            DataClassification.Public, false, false);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Theory]
    [InlineData(DataClassification.Internal)]
    [InlineData(DataClassification.Confidential)]
    public void ValidateSensitiveDataAccess_NonPublicWithoutAccessControl_ReturnsError(
        DataClassification classification)
    {
        // Act
        var result = _validator.ValidateSensitiveDataAccess(classification, false, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AC-SENSITIVE-001");
    }

    [Fact]
    public void ValidateSensitiveDataAccess_RestrictedWithoutApproval_ReturnsError()
    {
        // Act
        var result = _validator.ValidateSensitiveDataAccess(
            DataClassification.Restricted, true, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AC-SENSITIVE-002");
    }

    [Fact]
    public void ValidateMFA_EnabledForSensitive_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateMFA(true, true);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateMFA_NotEnabled_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidateMFA(false, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("AC-MFA-001");
    }

    [Fact]
    public void ValidateMFA_NotRequiredForSensitive_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidateMFA(true, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("AC-MFA-002");
    }

    [Fact]
    public void ValidateAccessReviews_ConductedFrequently_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateAccessReviews(true, 90);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateAccessReviews_NotConducted_ReturnsError()
    {
        // Act
        var result = _validator.ValidateAccessReviews(false, 0);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AC-REVIEWS-001");
    }

    [Fact]
    public void ValidateAccessReviews_InfrequentReviews_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidateAccessReviews(true, 120);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("AC-REVIEWS-002");
    }

    [Fact]
    public void ValidateAccessReviews_DisabledInOptions_ReturnsCompliant()
    {
        // Arrange
        _options.AccessControl.RequireAccessReviews = false;

        // Act
        var result = _validator.ValidateAccessReviews(false, 0);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }
}
