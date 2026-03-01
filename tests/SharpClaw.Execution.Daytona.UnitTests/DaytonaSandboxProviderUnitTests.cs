using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Daytona;

namespace SharpClaw.Execution.Daytona.UnitTests;

public class DaytonaSandboxProviderUnitTests
{
    [Fact]
    public void Name_IsDaytona()
    {
        var provider = new DaytonaSandboxProvider(
            NullLogger<DaytonaSandboxProvider>.Instance,
            serverUrl: "http://localhost",
            apiKey: "test-api-key");
        Assert.Equal("daytona", provider.Name);
    }

    [Fact]
    public async Task StartAsync_ReturnsDaytonaPrefixedHandle()
    {
        var provider = new DaytonaSandboxProvider(
            NullLogger<DaytonaSandboxProvider>.Instance,
            serverUrl: "http://localhost",
            apiKey: "test-api-key");

        var handle = await provider.StartAsync();

        Assert.Equal("daytona", handle.Provider);
        Assert.StartsWith("sharpclaw-", handle.SandboxId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_RespectsCancellation()
    {
        var provider = new DaytonaSandboxProvider(
            NullLogger<DaytonaSandboxProvider>.Instance,
            serverUrl: "http://localhost",
            apiKey: "test-api-key");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.StartAsync(cts.Token));
    }
}
