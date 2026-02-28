using SharpClaw.Execution.Docker;

namespace SharpClaw.Execution.Docker.UnitTests;

public class DockerSandboxProviderUnitTests
{
    [Fact]
    public void Name_IsDind()
    {
        var provider = new DockerSandboxProvider();

        Assert.Equal("dind", provider.Name);
    }

    [Fact]
    public async Task StartAsync_ReturnsDindPrefixedHandle()
    {
        var provider = new DockerSandboxProvider();

        var handle = await provider.StartAsync();

        Assert.Equal("dind", handle.Provider);
        Assert.StartsWith("dind-", handle.SandboxId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_RespectsCancellation()
    {
        var provider = new DockerSandboxProvider();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.StartAsync(cts.Token));
    }
}
