using FluentAssertions;
using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Compliance.Validation;

namespace SharpClaw.Compliance.UnitTests.Validation;

public sealed class EncryptionValidatorTests
{
    private readonly EncryptionValidator _validator;
    private readonly ComplianceEngineOptions _options;

    public EncryptionValidatorTests()
    {
        _options = new ComplianceEngineOptions();
        _options.Encryption.RequireEncryptionAtRest = true;
        _options.Encryption.RequireEncryptionInTransit = true;
        _options.Encryption.MinimumKeyLength = 256;

        var optionsWrapper = Options.Create(_options);
        _validator = new EncryptionValidator(optionsWrapper);
    }

    [Fact]
    public void ValidateEncryptionAtRest_EncryptedWithValidAlgorithm_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateEncryptionAtRest(true, "AES-256-GCM", 256);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateEncryptionAtRest_NotEncrypted_ReturnsError()
    {
        // Act
        var result = _validator.ValidateEncryptionAtRest(false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("ENCRYPT-REST-001");
    }

    [Fact]
    public void ValidateEncryptionAtRest_DisabledInOptions_ReturnsCompliant()
    {
        // Arrange
        _options.Encryption.RequireEncryptionAtRest = false;

        // Act
        var result = _validator.ValidateEncryptionAtRest(false);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateEncryptionAtRest_WeakAlgorithm_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidateEncryptionAtRest(true, "DES", 256);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("ENCRYPT-REST-002");
    }

    [Fact]
    public void ValidateEncryptionAtRest_ShortKey_ReturnsError()
    {
        // Act
        var result = _validator.ValidateEncryptionAtRest(true, "AES-256-GCM", 128);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("ENCRYPT-REST-003");
    }

    [Fact]
    public void ValidateEncryptionInTransit_Encrypted_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidateEncryptionInTransit(true, "TLS 1.3");

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public void ValidateEncryptionInTransit_NotEncrypted_ReturnsError()
    {
        // Act
        var result = _validator.ValidateEncryptionInTransit(false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.RuleId.Should().Be("ENCRYPT-TRANSIT-001");
    }

    [Theory]
    [InlineData("SSL")]
    [InlineData("TLS 1.0")]
    [InlineData("TLS 1.1")]
    public void ValidateEncryptionInTransit_WeakProtocol_ReturnsWarning(string protocol)
    {
        // Act
        var result = _validator.ValidateEncryptionInTransit(true, protocol);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("ENCRYPT-TRANSIT-002");
    }

    [Fact]
    public void ValidatePiiEncryption_NotEncrypted_ReturnsError()
    {
        // Act
        var result = _validator.ValidatePiiEncryption(false);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("PII-ENCRYPT-001");
    }

    [Fact]
    public void ValidatePiiEncryption_WeakAlgorithm_ReturnsError()
    {
        // Act
        var result = _validator.ValidatePiiEncryption(true, "DES", "KMS");

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Error);
        result.RuleId.Should().Be("PII-ENCRYPT-002");
    }

    [Fact]
    public void ValidatePiiEncryption_NoKeyManagement_ReturnsWarning()
    {
        // Act
        var result = _validator.ValidatePiiEncryption(true, "AES-256-GCM", null);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Severity.Should().Be(ComplianceSeverity.Warning);
        result.RuleId.Should().Be("PII-ENCRYPT-003");
    }

    [Fact]
    public void ValidatePiiEncryption_ValidConfiguration_ReturnsCompliant()
    {
        // Act
        var result = _validator.ValidatePiiEncryption(true, "AES-256-GCM", "KMS");

        // Assert
        result.IsCompliant.Should().BeTrue();
    }
}
