using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using SharpClaw.Abstractions.Cloud;

namespace SharpClaw.Cloud.Azure.Secrets;

/// <summary>
/// Azure Key Vault implementation of <see cref="ISecretManager"/>.
/// </summary>
public sealed class AzureKeyVaultSecretManager : ISecretManager, IAsyncDisposable
{
    private readonly SecretClient _secretClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureKeyVaultSecretManager"/> class.
    /// </summary>
    /// <param name="credential">The Azure token credential.</param>
    /// <param name="options">The Key Vault options.</param>
    /// <exception cref="ArgumentNullException">Thrown when parameters are null.</exception>
    public AzureKeyVaultSecretManager(global::Azure.Core.TokenCredential credential, AzureKeyVaultOptions options)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(options);

        _secretClient = CreateSecretClient(credential, options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureKeyVaultSecretManager"/> class.
    /// </summary>
    /// <param name="secretClient">The Key Vault secret client.</param>
    public AzureKeyVaultSecretManager(SecretClient secretClient)
    {
        ArgumentNullException.ThrowIfNull(secretClient);
        _secretClient = secretClient;
    }

    /// <inheritdoc />
    public async Task<string> GetSecretAsync(string name, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateSecretName(name);

        try
        {
            Response<KeyVaultSecret> response = await _secretClient
                .GetSecretAsync(name, cancellationToken: ct)
                .ConfigureAwait(false);
            return response.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"Secret '{name}' not found in Key Vault.", ex);
        }
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(string name, string value, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateSecretName(name);
        ArgumentException.ThrowIfNullOrEmpty(value);

        await _secretClient.SetSecretAsync(name, value, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RotateSecretAsync(string name, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateSecretName(name);

        // Get current secret to retrieve metadata
        Response<KeyVaultSecret> response;
        try
        {
            response = await _secretClient
                .GetSecretAsync(name, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"Secret '{name}' not found in Key Vault. Cannot rotate non-existent secret.", ex);
        }

        var currentSecret = response.Value;

        // Generate a new random value
        var newValue = GenerateSecureRandomValue();

        // Set the new secret value
        await _secretClient.SetSecretAsync(name, newValue, ct).ConfigureAwait(false);

        // Optionally disable old versions - for now we just create a new version
        // Azure Key Vault automatically manages versions
    }

    /// <summary>
    /// Creates a SecretClient based on the provided options.
    /// </summary>
    private static SecretClient CreateSecretClient(
        global::Azure.Core.TokenCredential credential,
        AzureKeyVaultOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.VaultUri);

        var clientOptions = new SecretClientOptions
        {
            Retry =
            {
                Delay = options.RetryDelay,
                MaxDelay = options.MaxRetryDelay,
                MaxRetries = options.MaxRetries
            }
        };

        return new SecretClient(options.VaultUri, credential, clientOptions);
    }

    /// <summary>
    /// Validates the secret name.
    /// </summary>
    private static void ValidateSecretName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Key Vault secret name rules: 1-127 characters, alphanumeric and hyphens
        if (name.Length > 127)
        {
            throw new ArgumentException("Secret name must be 127 characters or less.", nameof(name));
        }
    }

    /// <summary>
    /// Generates a secure random value for secret rotation.
    /// </summary>
    private static string GenerateSecureRandomValue()
    {
        const int length = 64;
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var buffer = new char[length];

        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);

        for (int i = 0; i < length; i++)
        {
            buffer[i] = chars[bytes[i] % chars.Length];
        }

        return new string(buffer);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            _disposed = true;
        }
    }

    /// <summary>
    /// Disposes the resources asynchronously.
    /// </summary>
    private async ValueTask DisposeAsyncCore()
    {
        // SecretClient doesn't implement IAsyncDisposable
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
