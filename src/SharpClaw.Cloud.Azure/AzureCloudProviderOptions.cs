using SharpClaw.Cloud.Azure.Auth;
using SharpClaw.Cloud.Azure.Cache;
using SharpClaw.Cloud.Azure.Secrets;
using SharpClaw.Cloud.Azure.Storage;

namespace SharpClaw.Cloud.Azure;

/// <summary>
/// Configuration options for the Azure Cloud Provider.
/// </summary>
public sealed class AzureCloudProviderOptions
{
    /// <summary>
    /// Gets or sets the Azure tenant ID for authentication.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the Azure subscription ID.
    /// </summary>
    public string? SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the service principal credentials for authentication.
    /// </summary>
    public ServicePrincipalCredentials? ServicePrincipal { get; set; }

    /// <summary>
    /// Gets or sets the managed identity options for authentication.
    /// </summary>
    public ManagedIdentityOptions? ManagedIdentity { get; set; }

    /// <summary>
    /// Gets or sets the Azure Blob Storage options.
    /// </summary>
    public AzureBlobStorageOptions? BlobStorage { get; set; }

    /// <summary>
    /// Gets or sets the Azure Key Vault options.
    /// </summary>
    public AzureKeyVaultOptions? KeyVault { get; set; }

    /// <summary>
    /// Gets or sets the Azure Redis Cache options.
    /// </summary>
    public AzureRedisCacheOptions? RedisCache { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable Azure Monitor telemetry.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Gets or sets the Application Insights connection string.
    /// </summary>
    public string? ApplicationInsightsConnectionString { get; set; }
}
