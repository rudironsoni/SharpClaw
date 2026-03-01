using Azure.Core;
using Azure.Identity;

namespace SharpClaw.Cloud.Azure.Auth;

/// <summary>
/// Factory for creating Azure credential instances based on configuration.
/// </summary>
public static class AzureCredentialFactory
{
    /// <summary>
    /// Creates a TokenCredential based on the provided options.
    /// </summary>
    /// <param name="options">The Azure cloud provider options.</param>
    /// <returns>A TokenCredential instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid authentication configuration is found.</exception>
    public static TokenCredential CreateCredential(AzureCloudProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Prefer managed identity when configured
        if (options.ManagedIdentity is not null)
        {
            return CreateManagedIdentityCredential(options.ManagedIdentity);
        }

        // Fall back to service principal
        if (options.ServicePrincipal is not null)
        {
            return CreateServicePrincipalCredential(options.ServicePrincipal);
        }

        // Default to DefaultAzureCredential which tries multiple auth methods
        return new DefaultAzureCredential();
    }

    /// <summary>
    /// Creates a ManagedIdentityCredential or DefaultAzureCredential based on options.
    /// </summary>
    /// <param name="options">The managed identity options.</param>
    /// <returns>A TokenCredential instance.</returns>
    private static TokenCredential CreateManagedIdentityCredential(ManagedIdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.UseSystemAssignedIdentity)
        {
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeManagedIdentityCredential = false,
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeAzureCliCredential = true,
                ExcludeAzurePowerShellCredential = true,
                ExcludeAzureDeveloperCliCredential = true,
                ExcludeInteractiveBrowserCredential = true
            });
        }

        // User-assigned managed identity
        if (!string.IsNullOrEmpty(options.UserAssignedIdentityClientId))
        {
            return new ManagedIdentityCredential(options.UserAssignedIdentityClientId);
        }

        if (!string.IsNullOrEmpty(options.UserAssignedIdentityResourceId))
        {
            return new ManagedIdentityCredential(new ResourceIdentifier(options.UserAssignedIdentityResourceId));
        }

        throw new InvalidOperationException(
            "ManagedIdentityOptions requires either UserAssignedIdentityClientId or UserAssignedIdentityResourceId " +
            "when UseSystemAssignedIdentity is false.");
    }

    /// <summary>
    /// Creates a ClientSecretCredential for service principal authentication.
    /// </summary>
    /// <param name="credentials">The service principal credentials.</param>
    /// <returns>A TokenCredential instance.</returns>
    private static TokenCredential CreateServicePrincipalCredential(ServicePrincipalCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (string.IsNullOrEmpty(credentials.ClientId))
        {
            throw new InvalidOperationException("ServicePrincipalCredentials.ClientId is required.");
        }

        if (string.IsNullOrEmpty(credentials.ClientSecret))
        {
            throw new InvalidOperationException("ServicePrincipalCredentials.ClientSecret is required.");
        }

        if (string.IsNullOrEmpty(credentials.TenantId))
        {
            throw new InvalidOperationException("ServicePrincipalCredentials.TenantId is required.");
        }

        return new ClientSecretCredential(
            credentials.TenantId,
            credentials.ClientId,
            credentials.ClientSecret);
    }
}
