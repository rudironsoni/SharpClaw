using SharpClaw.Execution.Podman;

namespace SharpClaw.Execution.Podman.UnitTests;

public class PodmanSandboxProviderUnitTests
{
    [Fact]
    public void Name_IsPodman()
    {
        var provider = new PodmanSandboxProvider();
        Assert.Equal("podman", provider.Name);
    }

    [Fact]
    public async Task StartAsync_ReturnsPodmanPrefixedHandle()
    {
        var provider = new PodmanSandboxProvider();

        var handle = await provider.StartAsync();

        Assert.Equal("podman", handle.Provider);
        Assert.StartsWith("podman-", handle.SandboxId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_RespectsCancellation()
    {
        var provider = new PodmanSandboxProvider();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.StartAsync(cts.Token));
    }
}
