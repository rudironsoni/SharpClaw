# SharpClaw.Cloud.Azure

Azure Cloud Provider implementation for SharpClaw.

## Overview

This package provides Azure-native implementations of the SharpClaw cloud abstractions:

- **Azure Blob Storage** - Implements `ICloudStorage` for object storage
- **Azure Key Vault** - Implements `ISecretManager` for secret management
- **Azure Redis Cache** - Implements `ICache` for distributed caching
- **Azure Monitor** - OpenTelemetry exporter for Application Insights

## Installation

```bash
dotnet add package SharpClaw.Cloud.Azure
```

## Configuration

### Service Principal Authentication

```csharp
var options = new AzureCloudProviderOptions
{
    ServicePrincipal = new ServicePrincipalCredentials
    {
        ClientId = "your-client-id",
        ClientSecret = "your-client-secret",
        TenantId = "your-tenant-id"
    },
    BlobStorage = new AzureBlobStorageOptions
    {
        ServiceUri = new Uri("https://youraccount.blob.core.windows.net"),
        ContainerName = "my-container"
    },
    KeyVault = new AzureKeyVaultOptions
    {
        VaultUri = new Uri("https://your-vault.vault.azure.net/")
    },
    RedisCache = new AzureRedisCacheOptions
    {
        ConnectionString = "your-redis-connection-string"
    }
};

builder.Services.AddAzureCloudProvider(options);
```

### Managed Identity Authentication

```csharp
var options = new AzureCloudProviderOptions
{
    ManagedIdentity = new ManagedIdentityOptions
    {
        UseSystemAssignedIdentity = true
        // Or for user-assigned:
        // UseSystemAssignedIdentity = false,
        // UserAssignedIdentityClientId = "your-client-id"
    },
    BlobStorage = new AzureBlobStorageOptions { ... },
    KeyVault = new AzureKeyVaultOptions { ... },
    RedisCache = new AzureRedisCacheOptions { ... }
};

builder.Services.AddAzureCloudProvider(options);
```

## Usage

```csharp
public class MyService
{
    private readonly ICloudProvider _cloudProvider;

    public MyService(ICloudProvider cloudProvider)
    {
        _cloudProvider = cloudProvider;
    }

    public async Task<string> GetSecretAsync(string name)
    {
        var secretManager = _cloudProvider.CreateSecretManager();
        return await secretManager.GetSecretAsync(name);
    }

    public async Task<T?> GetCachedAsync<T>(string key)
    {
        var cache = _cloudProvider.CreateCache();
        return await cache.GetAsync<T>(key);
    }

    public async Task SetCachedAsync<T>(string key, T value)
    {
        var cache = _cloudProvider.CreateCache();
        await cache.SetAsync(key, value, new CacheExpiration(
            AbsoluteExpiration: TimeSpan.FromHours(1)));
    }
}
```

## NuGet Dependencies

- Azure.Storage.Blobs
- Azure.Security.KeyVault.Secrets
- Azure.Identity
- Microsoft.Extensions.Caching.StackExchangeRedis
- StackExchange.Redis
- OpenTelemetry

## License

MIT License
