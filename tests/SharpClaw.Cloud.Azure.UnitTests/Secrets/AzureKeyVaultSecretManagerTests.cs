using Azure;
using Azure.Security.KeyVault.Secrets;
using NSubstitute;
using SharpClaw.Cloud.Azure.Secrets;
using Xunit;

namespace SharpClaw.Cloud.Azure.UnitTests.Secrets;

public class AzureKeyVaultSecretManagerTests
{
    private readonly SecretClient _mockSecretClient;

    public AzureKeyVaultSecretManagerTests()
    {
        _mockSecretClient = Substitute.For<SecretClient>();
    }

    [Fact]
    public void Constructor_WithNullSecretClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AzureKeyVaultSecretManager(null!));
    }

    [Fact]
    public async Task GetSecretAsync_WithNullName_ThrowsArgumentNullException()
    {
        var manager = new AzureKeyVaultSecretManager(_mockSecretClient);

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.GetSecretAsync(null!));
    }

    [Fact]
    public async Task GetSecretAsync_WithEmptyName_ThrowsArgumentException()
    {
        var manager = new AzureKeyVaultSecretManager(_mockSecretClient);

        await Assert.ThrowsAsync<ArgumentException>(() => manager.GetSecretAsync(""));
    }

    [Fact]
    public async Task SetSecretAsync_WithNullName_ThrowsArgumentNullException()
    {
        var manager = new AzureKeyVaultSecretManager(_mockSecretClient);

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.SetSecretAsync(null!, "value"));
    }

    [Fact]
    public async Task SetSecretAsync_WithNullValue_ThrowsArgumentNullException()
    {
        var manager = new AzureKeyVaultSecretManager(_mockSecretClient);

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.SetSecretAsync("name", null!));
    }

    [Fact]
    public async Task RotateSecretAsync_WithNullName_ThrowsArgumentNullException()
    {
        var manager = new AzureKeyVaultSecretManager(_mockSecretClient);

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.RotateSecretAsync(null!));
    }

    [Fact]
    public async Task RotateSecretAsync_WithLongName_ThrowsArgumentException()
    {
        var manager = new AzureKeyVaultSecretManager(_mockSecretClient);
        var longName = new string('a', 128);

        await Assert.ThrowsAsync<ArgumentException>(() => manager.RotateSecretAsync(longName));
    }
}
