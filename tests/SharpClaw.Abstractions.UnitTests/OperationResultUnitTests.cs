using SharpClaw.Abstractions;

namespace SharpClaw.Abstractions.UnitTests;

public class OperationResultUnitTests
{
    [Fact]
    public void Success_SetsSucceededAndClearsError()
    {
        var result = OperationResult.Success();

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_SetsErrorAndSucceededFalse()
    {
        var result = OperationResult.Failure("agent-timeout");

        Assert.False(result.Succeeded);
        Assert.Equal("agent-timeout", result.Error);
    }
}
