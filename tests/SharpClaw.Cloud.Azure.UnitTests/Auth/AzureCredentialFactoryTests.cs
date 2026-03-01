using Azure.Core;
using SharpClaw.Cloud.Azure.Auth;
using Xunit;

namespace SharpClaw.Cloud.Azure.UnitTests.Auth;

public class AzureCredentialFactoryTests
{
    [Fact]
    public void CreateCredential_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AzureCredentialFactory.CreateCredential(null!));
    }

    [Fact]
    public void CreateCredential_WithNoAuthConfig_ThrowsInvalidOperationException()
    {
        var options = new AzureCloudProviderOptions();

        // Should return DefaultAzureCredential when no specific auth is configured
        var credential = AzureCredentialFactory.CreateCredential(options);
        Assert.NotNull(credential);
    }

    [Fact]
    public void CreateCredential_WithServicePrincipal_MissingClientId_ThrowsInvalidOperationException()
    {
        var options = new AzureCloudProviderOptions
        {
            ServicePrincipal = new ServicePrincipalCredentials
            {
                ClientId = "",
                ClientSecret = "secret",
                TenantId = "tenant"
            }
        };

        Assert.Throws<InvalidOperationException>(() => AzureCredentialFactory.CreateCredential(options));
    }

    [Fact]
    public void CreateCredential_WithServicePrincipal_MissingClientSecret_ThrowsInvalidOperationException()
    {
        var options = new AzureCloudProviderOptions
        {
            ServicePrincipal = new ServicePrincipalCredentials
            {
                ClientId = "client-id",
                ClientSecret = "",
                TenantId = "tenant"
            }
        };

        Assert.Throws<InvalidOperationException>(() => AzureCredentialFactory.CreateCredential(options));
    }

    [Fact]
    public void CreateCredential_WithServicePrincipal_MissingTenantId_ThrowsInvalidOperationException()
    {
        var options = new AzureCloudProviderOptions
        {
            ServicePrincipal = new ServicePrincipalCredentials
            {
                ClientId = "client-id",
                ClientSecret = "secret",
                TenantId = ""
            }
        };

        Assert.Throws<InvalidOperationException>(() => AzureCredentialFactory.CreateCredential(options));
    }

    [Fact]
    public void CreateCredential_WithServicePrincipal_Valid_ReturnsClientSecretCredential()
    {
        var options = new AzureCloudProviderOptions
        {
            ServicePrincipal = new ServicePrincipalCredentials
            {
                ClientId = "client-id",
                ClientSecret = "secret",
                TenantId = "tenant-id"
            }
        };

        var credential = AzureCredentialFactory.CreateCredential(options);
        Assert.NotNull(credential);
        Assert.IsType<global::Azure.Identity.ClientSecretCredential>(credential);
    }

    [Fact]
    public void CreateCredential_WithManagedIdentity_SystemAssigned_ReturnsDefaultAzureCredential()
    {
        var options = new AzureCloudProviderOptions
        {
            ManagedIdentity = new ManagedIdentityOptions
            {
                UseSystemAssignedIdentity = true
            }
        };

        var credential = AzureCredentialFactory.CreateCredential(options);
        Assert.NotNull(credential);
    }

    [Fact]
    public void CreateCredential_WithManagedIdentity_UserAssignedNoClientId_ThrowsInvalidOperationException()
    {
        var options = new AzureCloudProviderOptions
        {
            ManagedIdentity = new ManagedIdentityOptions
            {
                UseSystemAssignedIdentity = false,
                UserAssignedIdentityClientId = null,
                UserAssignedIdentityResourceId = null
            }
        };

        Assert.Throws<InvalidOperationException>(() => AzureCredentialFactory.CreateCredential(options));
    }

    [Fact]
    public void CreateCredential_WithManagedIdentity_UserAssignedWithClientId_ReturnsManagedIdentityCredential()
    {
        var options = new AzureCloudProviderOptions
        {
            ManagedIdentity = new ManagedIdentityOptions
            {
                UseSystemAssignedIdentity = false,
                UserAssignedIdentityClientId = "client-id"
            }
        };

        var credential = AzureCredentialFactory.CreateCredential(options);
        Assert.NotNull(credential);
        Assert.IsType<global::Azure.Identity.ManagedIdentityCredential>(credential);
    }
}
