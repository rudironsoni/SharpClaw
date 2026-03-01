using Microsoft.Extensions.Options;
using SharpClaw.Compliance.Abstractions;

namespace SharpClaw.Compliance.Validation;

/// <summary>
/// Validates encryption requirements against compliance standards.
/// </summary>
public sealed class EncryptionValidator
{
    private readonly ComplianceEngineOptions _options;

    public EncryptionValidator(IOptions<ComplianceEngineOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Validates encryption at rest requirements.
    /// </summary>
    /// <param name="isEncrypted">Whether data is encrypted at rest.</param>
    /// <param name="algorithm">The encryption algorithm used.</param>
    /// <param name="keyLength">The encryption key length in bits.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateEncryptionAtRest(
        bool isEncrypted,
        string? algorithm = null,
        int? keyLength = null)
    {
        if (!_options.Encryption.RequireEncryptionAtRest)
        {
            return ComplianceValidationResult.Compliant("ENCRYPT-REST-001");
        }

        if (!isEncrypted)
        {
            return ComplianceValidationResult.NonCompliant(
                "ENCRYPT-REST-001",
                "Data is not encrypted at rest",
                ComplianceSeverity.Error,
                "Enable encryption at rest for all stored data");
        }

        if (algorithm != null && !IsAllowedAlgorithm(algorithm))
        {
            return ComplianceValidationResult.NonCompliant(
                "ENCRYPT-REST-002",
                $"Encryption algorithm '{algorithm}' is not in the allowed list",
                ComplianceSeverity.Warning,
                $"Use one of the approved algorithms: {string.Join(", ", _options.Encryption.AllowedAlgorithms)}");
        }

        if (keyLength.HasValue && keyLength.Value < _options.Encryption.MinimumKeyLength)
        {
            return ComplianceValidationResult.NonCompliant(
                "ENCRYPT-REST-003",
                $"Encryption key length ({keyLength.Value} bits) is below minimum ({_options.Encryption.MinimumKeyLength} bits)",
                ComplianceSeverity.Error,
                $"Use keys with at least {_options.Encryption.MinimumKeyLength} bits");
        }

        return ComplianceValidationResult.Compliant("ENCRYPT-REST-001");
    }

    /// <summary>
    /// Validates encryption in transit requirements.
    /// </summary>
    /// <param name="isEncrypted">Whether communication is encrypted.</param>
    /// <param name="protocol">The protocol used (e.g., TLS 1.3).</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidateEncryptionInTransit(
        bool isEncrypted,
        string? protocol = null)
    {
        if (!_options.Encryption.RequireEncryptionInTransit)
        {
            return ComplianceValidationResult.Compliant("ENCRYPT-TRANSIT-001");
        }

        if (!isEncrypted)
        {
            return ComplianceValidationResult.NonCompliant(
                "ENCRYPT-TRANSIT-001",
                "Data transmission is not encrypted",
                ComplianceSeverity.Error,
                "Enable TLS/SSL for all data transmission");
        }

        if (protocol != null && IsWeakProtocol(protocol))
        {
            return ComplianceValidationResult.NonCompliant(
                "ENCRYPT-TRANSIT-002",
                $"Protocol '{protocol}' has known vulnerabilities",
                ComplianceSeverity.Warning,
                "Upgrade to TLS 1.2 or higher");
        }

        return ComplianceValidationResult.Compliant("ENCRYPT-TRANSIT-001");
    }

    /// <summary>
    /// Validates PII-specific encryption requirements.
    /// </summary>
    /// <param name="isEncrypted">Whether PII is encrypted.</param>
    /// <param name="algorithm">The encryption algorithm.</param>
    /// <param name="keyManagement">The key management approach.</param>
    /// <returns>Validation result.</returns>
    public ComplianceValidationResult ValidatePiiEncryption(
        bool isEncrypted,
        string? algorithm = null,
        string? keyManagement = null)
    {
        if (!isEncrypted)
        {
            return ComplianceValidationResult.NonCompliant(
                "PII-ENCRYPT-001",
                "PII data is not encrypted",
                ComplianceSeverity.Error,
                "Encrypt all PII data at rest and in transit");
        }

        if (algorithm != null && !IsStrongAlgorithmForPii(algorithm))
        {
            return ComplianceValidationResult.NonCompliant(
                "PII-ENCRYPT-002",
                $"Algorithm '{algorithm}' is not strong enough for PII",
                ComplianceSeverity.Error,
                "Use AES-256-GCM or RSA-4096 for PII encryption");
        }

        if (string.IsNullOrEmpty(keyManagement))
        {
            return ComplianceValidationResult.NonCompliant(
                "PII-ENCRYPT-003",
                "No key management strategy documented for PII encryption",
                ComplianceSeverity.Warning,
                "Implement a secure key management solution (HSM, KMS)");
        }

        return ComplianceValidationResult.Compliant("PII-ENCRYPT-001");
    }

    private bool IsAllowedAlgorithm(string algorithm)
    {
        return _options.Encryption.AllowedAlgorithms.Contains(algorithm, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsWeakProtocol(string protocol)
    {
        var weakProtocols = new[] { "SSL", "SSLv2", "SSLv3", "TLS 1.0", "TLS 1.1" };
        return weakProtocols.Contains(protocol, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsStrongAlgorithmForPii(string algorithm)
    {
        var strongAlgorithms = new[] { "AES-256-GCM", "AES-256-CBC", "RSA-4096", "ChaCha20-Poly1305" };
        return strongAlgorithms.Contains(algorithm, StringComparer.OrdinalIgnoreCase);
    }
}
