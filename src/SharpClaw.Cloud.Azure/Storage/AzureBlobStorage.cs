using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SharpClaw.Abstractions.Cloud;

namespace SharpClaw.Cloud.Azure.Storage;

/// <summary>
/// Azure Blob Storage implementation of <see cref="ICloudStorage"/>.
/// </summary>
public sealed class AzureBlobStorage : ICloudStorage, IAsyncDisposable
{
    private readonly BlobServiceClient _blobService;
    private readonly string _containerName;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobStorage"/> class.
    /// </summary>
    /// <param name="credential">The Azure token credential.</param>
    /// <param name="options">The blob storage options.</param>
    /// <exception cref="ArgumentException">Thrown when required options are missing.</exception>
    public AzureBlobStorage(global::Azure.Core.TokenCredential credential, AzureBlobStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(options);

        _blobService = CreateBlobServiceClient(credential, options);
        _containerName = ValidateContainerName(options.ContainerName);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobStorage"/> class.
    /// </summary>
    /// <param name="serviceClient">The blob service client.</param>
    /// <param name="containerName">The default container name.</param>
    public AzureBlobStorage(BlobServiceClient serviceClient, string containerName)
    {
        ArgumentNullException.ThrowIfNull(serviceClient);
        _blobService = serviceClient;
        _containerName = ValidateContainerName(containerName);
    }

    /// <inheritdoc />
    public async Task<Stream> GetObjectAsync(string bucket, string key, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateKey(key);

        var containerName = string.IsNullOrEmpty(bucket) ? _containerName : bucket;
        var container = _blobService.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(key);

        try
        {
            Response<BlobDownloadInfo> response = await blob.DownloadAsync(ct).ConfigureAwait(false);
            return response.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"Blob '{key}' not found in container '{containerName}'.", ex);
        }
    }

    /// <inheritdoc />
    public async Task PutObjectAsync(string bucket, string key, Stream data, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(data);
        ValidateKey(key);

        var containerName = string.IsNullOrEmpty(bucket) ? _containerName : bucket;

        // Ensure container exists
        var container = _blobService.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct).ConfigureAwait(false);

        var blob = container.GetBlobClient(key);
        await blob.UploadAsync(data, overwrite: true, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteObjectAsync(string bucket, string key, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateKey(key);

        var containerName = string.IsNullOrEmpty(bucket) ? _containerName : bucket;
        var container = _blobService.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(key);

        await blob.DeleteIfExistsAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateKey(key);

        var containerName = string.IsNullOrEmpty(bucket) ? _containerName : bucket;
        var container = _blobService.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(key);

        return await blob.ExistsAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a blob service client based on the provided options.
    /// </summary>
    private static BlobServiceClient CreateBlobServiceClient(
        global::Azure.Core.TokenCredential credential,
        AzureBlobStorageOptions options)
    {
        if (!string.IsNullOrEmpty(options.ConnectionString))
        {
            return new BlobServiceClient(options.ConnectionString);
        }

        if (options.ServiceUri is not null)
        {
            return new BlobServiceClient(options.ServiceUri, credential);
        }

        throw new ArgumentException(
            "Either ConnectionString or ServiceUri must be provided in AzureBlobStorageOptions.",
            nameof(options));
    }

    /// <summary>
    /// Validates the container name.
    /// </summary>
    private static string ValidateContainerName(string? containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            throw new ArgumentException("Container name is required.", nameof(containerName));
        }

        return containerName;
    }

    /// <summary>
    /// Validates the blob key.
    /// </summary>
    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
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
        // BlobServiceClient doesn't implement IAsyncDisposable, but we can dispose any streams
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
