namespace SharpClaw.Cloud.Azure.Secrets;

/// <summary>
/// Configuration options for Azure Key Vault.
/// </summary>
public sealed class AzureKeyVaultOptions
{
    /// <summary>
    /// Gets or sets the Key Vault URI.
    /// </summary>
    public required Uri VaultUri { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient failures.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay between retry attempts.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to enable certificate validation.
    /// </summary>
    public bool ValidateCertificates { get; set; } = true;
}
