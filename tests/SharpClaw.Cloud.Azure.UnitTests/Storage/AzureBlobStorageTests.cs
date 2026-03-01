using Azure;
using Azure.Storage.Blobs;
using NSubstitute;
using SharpClaw.Cloud.Azure.Storage;
using Xunit;

namespace SharpClaw.Cloud.Azure.UnitTests.Storage;

public class AzureBlobStorageTests
{
    private readonly BlobServiceClient _mockBlobServiceClient;
    private readonly BlobContainerClient _mockContainerClient;
    private readonly BlobClient _mockBlobClient;

    public AzureBlobStorageTests()
    {
        _mockBlobServiceClient = Substitute.For<BlobServiceClient>();
        _mockContainerClient = Substitute.For<BlobContainerClient>();
        _mockBlobClient = Substitute.For<BlobClient>();
    }

    [Fact]
    public void Constructor_WithNullServiceClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AzureBlobStorage(null!, "container"));
    }

    [Fact]
    public void Constructor_WithEmptyContainerName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AzureBlobStorage(_mockBlobServiceClient, ""));
    }

    [Fact]
    public void Constructor_WithNullContainerName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AzureBlobStorage(_mockBlobServiceClient, null!));
    }

    [Fact]
    public async Task ExistsAsync_WithNullKey_ThrowsArgumentNullException()
    {
        var storage = new AzureBlobStorage(_mockBlobServiceClient, "test-container");

        await Assert.ThrowsAsync<ArgumentNullException>(() => storage.ExistsAsync("bucket", null!));
    }

    [Fact]
    public async Task DeleteObjectAsync_WithNullKey_ThrowsArgumentNullException()
    {
        var storage = new AzureBlobStorage(_mockBlobServiceClient, "test-container");

        await Assert.ThrowsAsync<ArgumentNullException>(() => storage.DeleteObjectAsync("bucket", null!));
    }

    [Fact]
    public async Task GetObjectAsync_WithNullKey_ThrowsArgumentNullException()
    {
        var storage = new AzureBlobStorage(_mockBlobServiceClient, "test-container");

        await Assert.ThrowsAsync<ArgumentNullException>(() => storage.GetObjectAsync("bucket", null!));
    }

    [Fact]
    public async Task PutObjectAsync_WithNullKey_ThrowsArgumentNullException()
    {
        var storage = new AzureBlobStorage(_mockBlobServiceClient, "test-container");
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentNullException>(() => storage.PutObjectAsync("bucket", null!, stream));
    }

    [Fact]
    public async Task PutObjectAsync_WithNullStream_ThrowsArgumentNullException()
    {
        var storage = new AzureBlobStorage(_mockBlobServiceClient, "test-container");

        await Assert.ThrowsAsync<ArgumentNullException>(() => storage.PutObjectAsync("bucket", "key", null!));
    }
}
