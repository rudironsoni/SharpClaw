using Azure.Core;
using NSubstitute;
using SharpClaw.Abstractions.Cloud;
using SharpClaw.Cloud.Azure.Auth;
using SharpClaw.Cloud.Azure.Cache;
using SharpClaw.Cloud.Azure.Secrets;
using SharpClaw.Cloud.Azure.Storage;
using Xunit;

namespace SharpClaw.Cloud.Azure.UnitTests;

public class AzureCloudProviderTests
{
    private readonly TokenCredential _mockCredential;

    public AzureCloudProviderTests()
    {
        _mockCredential = Substitute.For<TokenCredential>();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AzureCloudProvider(null!));
    }

    [Fact]
    public void Constructor_WithNullOptionsAndCredential_ThrowsArgumentNullException()
    {
        var options = new AzureCloudProviderOptions();
        Assert.Throws<ArgumentNullException>(() => new AzureCloudProvider(options, null!));
    }

    [Fact]
    public void Constructor_WithNullCredential_ThrowsArgumentNullException()
    {
        var options = new AzureCloudProviderOptions();
        Assert.Throws<ArgumentNullException>(() => new AzureCloudProvider(options, null!));
    }

    [Fact]
    public void CreateStorage_WhenNotConfigured_ThrowsInvalidOperationException()
    {
        var options = new AzureCloudProviderOptions();
        var provider = new AzureCloudProvider(options, _mockCredential);

        Assert.Throws<InvalidOperationException>(() => provider.CreateStorage());
    }

    [Fact]
    public void CreateSecretManager_WhenNotConfigured_ThrowsInvalidOperationException()
    {
        var options = new AzureCloudProviderOptions();
        var provider = new AzureCloudProvider(options, _mockCredential);

        Assert.Throws<InvalidOperationException>(() => provider.CreateSecretManager());
    }

    [Fact]
    public void CreateCache_WhenNotConfigured_ThrowsInvalidOperationException()
    {
        var options = new AzureCloudProviderOptions();
        var provider = new AzureCloudProvider(options, _mockCredential);

        Assert.Throws<InvalidOperationException>(() => provider.CreateCache());
    }

    [Fact]
    public void CreateStorage_WithBlobStorageConfigured_ReturnsAzureBlobStorage()
    {
        var options = new AzureCloudProviderOptions
        {
            BlobStorage = new AzureBlobStorageOptions
            {
                ConnectionString = "UseDevelopmentStorage=true",
                ContainerName = "test-container"
            }
        };
        var provider = new AzureCloudProvider(options, _mockCredential);

        var storage = provider.CreateStorage();

        Assert.NotNull(storage);
        Assert.IsType<AzureBlobStorage>(storage);
    }

    [Fact]
    public void CreateStorage_CalledTwice_ReturnsSameInstance()
    {
        var options = new AzureCloudProviderOptions
        {
            BlobStorage = new AzureBlobStorageOptions
            {
                ConnectionString = "UseDevelopmentStorage=true",
                ContainerName = "test-container"
            }
        };
        var provider = new AzureCloudProvider(options, _mockCredential);

        var storage1 = provider.CreateStorage();
        var storage2 = provider.CreateStorage();

        Assert.Same(storage1, storage2);
    }

    [Fact]
    public void CreateSecretManager_WithKeyVaultConfigured_ReturnsAzureKeyVaultSecretManager()
    {
        var options = new AzureCloudProviderOptions
        {
            KeyVault = new AzureKeyVaultOptions
            {
                VaultUri = new Uri("https://test-vault.vault.azure.net/")
            }
        };
        var provider = new AzureCloudProvider(options, _mockCredential);

        var secretManager = provider.CreateSecretManager();

        Assert.NotNull(secretManager);
        Assert.IsType<AzureKeyVaultSecretManager>(secretManager);
    }

    [Fact]
    public void CreateSecretManager_CalledTwice_ReturnsSameInstance()
    {
        var options = new AzureCloudProviderOptions
        {
            KeyVault = new AzureKeyVaultOptions
            {
                VaultUri = new Uri("https://test-vault.vault.azure.net/")
            }
        };
        var provider = new AzureCloudProvider(options, _mockCredential);

        var manager1 = provider.CreateSecretManager();
        var manager2 = provider.CreateSecretManager();

        Assert.Same(manager1, manager2);
    }

    [Fact]
    public void CreateStorage_AfterDispose_ThrowsObjectDisposedException()
    {
        var options = new AzureCloudProviderOptions
        {
            BlobStorage = new AzureBlobStorageOptions
            {
                ConnectionString = "UseDevelopmentStorage=true",
                ContainerName = "test-container"
            }
        };
        var provider = new AzureCloudProvider(options, _mockCredential);
        provider.Dispose();

        Assert.Throws<ObjectDisposedException>(() => provider.CreateStorage());
    }

    [Fact]
    public void CreateSecretManager_AfterDispose_ThrowsObjectDisposedException()
    {
        var options = new AzureCloudProviderOptions
        {
            KeyVault = new AzureKeyVaultOptions
            {
                VaultUri = new Uri("https://test-vault.vault.azure.net/")
            }
        };
        var provider = new AzureCloudProvider(options, _mockCredential);
        provider.Dispose();

        Assert.Throws<ObjectDisposedException>(() => provider.CreateSecretManager());
    }

    [Fact]
    public void CreateCache_AfterDispose_ThrowsObjectDisposedException()
    {
        var options = new AzureCloudProviderOptions
        {
            RedisCache = new AzureRedisCacheOptions
            {
                ConnectionString = "localhost:6379"
            }
        };
        var provider = new AzureCloudProvider(options, _mockCredential);
        provider.Dispose();

        Assert.Throws<ObjectDisposedException>(() => provider.CreateCache());
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var options = new AzureCloudProviderOptions();
        var provider = new AzureCloudProvider(options, _mockCredential);

        provider.Dispose();
        provider.Dispose(); // Should not throw
    }

    [Fact]
    public void Implements_ICloudProvider()
    {
        var options = new AzureCloudProviderOptions();
        var provider = new AzureCloudProvider(options, _mockCredential);

        Assert.IsAssignableFrom<ICloudProvider>(provider);
    }

    [Fact]
    public void Implements_IDisposable()
    {
        var options = new AzureCloudProviderOptions();
        var provider = new AzureCloudProvider(options, _mockCredential);

        Assert.IsAssignableFrom<IDisposable>(provider);
    }
}

public class ServicePrincipalCredentialsTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var credentials = new ServicePrincipalCredentials
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            TenantId = "tenant-id"
        };

        Assert.Equal("client-id", credentials.ClientId);
        Assert.Equal("client-secret", credentials.ClientSecret);
        Assert.Equal("tenant-id", credentials.TenantId);
    }

}

public class ManagedIdentityOptionsTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var options = new ManagedIdentityOptions
        {
            UseSystemAssignedIdentity = true,
            UserAssignedIdentityClientId = "client-id",
            UserAssignedIdentityResourceId = "/subscriptions/test"
        };

        Assert.True(options.UseSystemAssignedIdentity);
        Assert.Equal("client-id", options.UserAssignedIdentityClientId);
        Assert.Equal("/subscriptions/test", options.UserAssignedIdentityResourceId);
    }
}

public class AzureBlobStorageOptionsTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var options = new AzureBlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ServiceUri = new Uri("https://test.blob.core.windows.net"),
            ContainerName = "test-container"
        };

        Assert.Equal("UseDevelopmentStorage=true", options.ConnectionString);
        Assert.Equal(new Uri("https://test.blob.core.windows.net"), options.ServiceUri);
        Assert.Equal("test-container", options.ContainerName);
    }
}

public class AzureKeyVaultOptionsTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var options = new AzureKeyVaultOptions
        {
            VaultUri = new Uri("https://test-vault.vault.azure.net/"),
            MaxRetries = 3,
            RetryDelay = TimeSpan.FromSeconds(1),
            MaxRetryDelay = TimeSpan.FromMinutes(1)
        };

        Assert.Equal(new Uri("https://test-vault.vault.azure.net/"), options.VaultUri);
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(1), options.RetryDelay);
        Assert.Equal(TimeSpan.FromMinutes(1), options.MaxRetryDelay);
    }
}

public class AzureRedisCacheOptionsTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var options = new AzureRedisCacheOptions
        {
            ConnectionString = "localhost:6379",
            InstanceName = "test-instance",
            DefaultExpiration = TimeSpan.FromMinutes(30),
            AbortOnConnectFail = false,
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            ConnectRetryCount = 3,
            UseSsl = true,
            MaxPoolSize = 100
        };

        Assert.Equal("localhost:6379", options.ConnectionString);
        Assert.Equal("test-instance", options.InstanceName);
        Assert.Equal(TimeSpan.FromMinutes(30), options.DefaultExpiration);
        Assert.False(options.AbortOnConnectFail);
        Assert.Equal(5000, options.ConnectTimeout);
        Assert.Equal(5000, options.SyncTimeout);
        Assert.Equal(3, options.ConnectRetryCount);
        Assert.True(options.UseSsl);
        Assert.Equal(100, options.MaxPoolSize);
    }
}

public class AzureCloudProviderOptionsTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var blobOptions = new AzureBlobStorageOptions { ContainerName = "test" };
        var keyVaultOptions = new AzureKeyVaultOptions { VaultUri = new Uri("https://test.vault.azure.net") };
        var redisOptions = new AzureRedisCacheOptions { ConnectionString = "localhost" };
        var servicePrincipal = new ServicePrincipalCredentials { ClientId = "test", ClientSecret = "secret", TenantId = "tenant" };
        var managedIdentity = new ManagedIdentityOptions { UseSystemAssignedIdentity = true };

        var options = new AzureCloudProviderOptions
        {
            BlobStorage = blobOptions,
            KeyVault = keyVaultOptions,
            RedisCache = redisOptions,
            ServicePrincipal = servicePrincipal,
            ManagedIdentity = managedIdentity
        };

        Assert.Same(blobOptions, options.BlobStorage);
        Assert.Same(keyVaultOptions, options.KeyVault);
        Assert.Same(redisOptions, options.RedisCache);
        Assert.Same(servicePrincipal, options.ServicePrincipal);
        Assert.Same(managedIdentity, options.ManagedIdentity);
    }
}
