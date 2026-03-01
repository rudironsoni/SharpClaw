namespace SharpClaw.Cloud.Azure.Storage;

/// <summary>
/// Configuration options for Azure Blob Storage.
/// </summary>
public sealed class AzureBlobStorageOptions
{
    /// <summary>
    /// Gets or sets the Azure Blob Storage connection string.
    /// If set, takes precedence over account name and endpoint.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the blob service endpoint URL.
    /// </summary>
    public Uri? ServiceUri { get; set; }

    /// <summary>
    /// Gets or sets the default container name for blob operations.
    /// </summary>
    public string? ContainerName { get; set; }

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
}
