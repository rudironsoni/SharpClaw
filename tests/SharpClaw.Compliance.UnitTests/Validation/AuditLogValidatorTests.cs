using FluentAssertions;
using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Compliance.Validation;

namespace SharpClaw.Compliance.UnitTests.Validation;

public sealed class AuditLogValidatorTests
{
    private readonly AuditLogValidator _validator;
    private readonly ComplianceEngineOptions _options;

    public AuditLogValidatorTests()
    {
        _options = new ComplianceEngineOptions();
        _options.Audit.RequireAuditLogging = true;
        _options.Audit.RequireImmutableLogs = true;
        _options.Audit.LogDataChanges = true;
        _options.Audit.LogPiiAccess = true;
        _options.DataRetention.AuditLogRetentionDays = 2555;

        var optionsWrapper = Options.Create(_options);
        _validator = new AuditLogValidator(optionsWrapper);
    }

    [Fact]
    public void ValidateAuditLoggingEnabled_Enabled_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateAuditLoggingEnabled(true);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateAuditLoggingEnabled_Disabled_ReturnsError()
    {
        // Act
        var result = _validator.ValidateAuditLoggingEnabled(false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AUDIT-001");
    }

    [Fact]
    public void ValidateAuditLoggingEnabled_DisabledInOptions_ReturnsCompliant()
    {
        // Arrange
        _options.Audit.RequireAuditLogging = false;

        // Act
        var result = _validator.ValidateAuditLoggingEnabled(false);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateAuditImmutability_ImmutableWithTamperDetection_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateAuditImmutability(true, true);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateAuditImmutability_NotImmutable_ReturnsError()
    {
        // Act
        var result = _validator.ValidateAuditImmutability(false, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AUDIT-IMMUTABLE-001");
    }

    [Fact]
    public void ValidateAuditImmutability_NoTamperDetection_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidateAuditImmutability(true, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("AUDIT-IMMUTABLE-002");
    }

    [Fact]
    public void ValidatePiiAccessLogging_LoggedWithContext_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidatePiiAccessLogging(true, true);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidatePiiAccessLogging_NotLogged_ReturnsError()
    {
        // Act
        var result = _validator.ValidatePiiAccessLogging(false, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AUDIT-PII-001");
    }

    [Fact]
    public void ValidatePiiAccessLogging_MissingUserContext_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidatePiiAccessLogging(true, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("AUDIT-PII-002");
    }

    [Fact]
    public void ValidateDataChangeLogging_LoggedWithBeforeAfter_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateDataChangeLogging(true, true);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateDataChangeLogging_NotLogged_ReturnsError()
    {
        // Act
        var result = _validator.ValidateDataChangeLogging(false, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AUDIT-CHANGES-001");
    }

    [Fact]
    public void ValidateDataChangeLogging_MissingBeforeAfter_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidateDataChangeLogging(true, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("AUDIT-CHANGES-002");
    }

    [Fact]
    public void ValidateLogRetention_SufficientRetention_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateLogRetention(2555);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateLogRetention_InsufficientRetention_ReturnsError()
    {
        // Act
        var result = _validator.ValidateLogRetention(100);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("AUDIT-RETENTION-001");
    }
}
