using FluentAssertions;
using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Compliance.Validation;

namespace SharpClaw.Compliance.UnitTests.Validation;

public sealed class DataRetentionValidatorTests
{
    private readonly DataRetentionValidator _validator;
    private readonly ComplianceEngineOptions _options;

    public DataRetentionValidatorTests()
    {
        _options = new ComplianceEngineOptions();
        _options.DataRetention.DefaultRetentionDays = 365;
        _options.DataRetention.PiiRetentionDays = 2555;

        var optionsWrapper = Options.Create(_options);
        _validator = new DataRetentionValidator(optionsWrapper);
    }

    [Theory]
    [InlineData(DataClassification.Public, 30, true)]
    [InlineData(DataClassification.Public, 29, false)]
    [InlineData(DataClassification.Internal, 90, true)]
    [InlineData(DataClassification.Internal, 89, false)]
    [InlineData(DataClassification.Confidential, 365, true)]
    [InlineData(DataClassification.Confidential, 364, false)]
    [InlineData(DataClassification.Restricted, 730, true)]
    [InlineData(DataClassification.Restricted, 729, false)]
    public void ValidateRetention_VariousClassifications_ReturnsExpectedResult(
        DataClassification classification,
        int retentionDays,
        bool expectedCompliant)
    {
        // Act
        var result = _validator.ValidateRetention(classification, retentionDays);

        // Assert
        result.IsCompliant.Should().Be(expectedCompliant);
    }

    [Fact]
    public void ValidateRetention_InsufficientRetention_ReturnsErrorWithRemediation()
    {
        // Act
        var result = _validator.ValidateRetention(DataClassification.Confidential, 100);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.Remediation.Should().Contain("365");
    }

    [Fact]
    public void ValidateAuditLogRetention_SufficientRetention_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateAuditLogRetention(2555);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateAuditLogRetention_InsufficientRetention_ReturnsNonCompliant()
    {
        // Act
        var result = _validator.ValidateAuditLogRetention(100);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Message.Should().Contain("100 days");
        result.Message.Should().Contain("2555 days");
    }

    [Fact]
    public void ValidatePiiRetention_NoConsent_ReturnsError()
    {
        // Act
        var result = _validator.ValidatePiiRetention(365, false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.RuleId.Should().Be("PII-RETENTION-001");
        result.Message.Should().Contain("consent");
    }

    [Fact]
    public void ValidatePiiRetention_ExcessiveRetention_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidatePiiRetention(3000, true);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("PII-RETENTION-002");
    }

    [Fact]
    public void ValidatePiiRetention_ValidRetentionWithConsent_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidatePiiRetention(365, true);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }
}
