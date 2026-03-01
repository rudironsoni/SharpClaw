using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Daytona;

namespace SharpClaw.Execution.Daytona.UnitTests;

public class DaytonaSandboxProviderUnitTests
{
    [Fact]
    public void Name_IsDaytona()
    {
        var provider = new DaytonaSandboxProvider(NullLogger<DaytonaSandboxProvider>.Instance);
        Assert.Equal("daytona", provider.Name);
    }

    [Fact]
    public async Task StartAsync_ReturnsDaytonaPrefixedHandle()
    {
        var provider = new DaytonaSandboxProvider(NullLogger<DaytonaSandboxProvider>.Instance);

        var handle = await provider.StartAsync();

        Assert.Equal("daytona", handle.Provider);
        Assert.StartsWith("daytona-", handle.SandboxId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_RespectsCancellation()
    {
        var provider = new DaytonaSandboxProvider(NullLogger<DaytonaSandboxProvider>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.StartAsync(cts.Token));
    }
}
