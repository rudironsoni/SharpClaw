using SharpClaw.OpenResponses.HttpApi;
using SharpClaw.Protocol.Contracts;

namespace SharpClaw.OpenResponses.UnitTests;

public class OpenResponsesValidatorUnitTests
{
    [Fact]
    public void Validate_ReturnsInvalidRequest_WhenRequestMissing()
    {
        var error = OpenResponsesValidator.Validate(null);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error!.Code);
    }

    [Fact]
    public void Validate_ReturnsInvalidRequest_WhenModelMissing()
    {
        var error = OpenResponsesValidator.Validate(new OpenResponsesRequest(
            Model: null,
            Input: new { text = "hello" }));

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error!.Code);
        Assert.Contains("Model", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ReturnsInvalidRequest_WhenInputMissing()
    {
        var error = OpenResponsesValidator.Validate(new OpenResponsesRequest(
            Model: "gpt-4o-mini",
            Input: null));

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error!.Code);
    }

    [Fact]
    public void Validate_ReturnsNull_WhenRequestValid()
    {
        var error = OpenResponsesValidator.Validate(new OpenResponsesRequest(
            Model: "gpt-4o-mini",
            Input: new { text = "hello" }));

        Assert.Null(error);
    }
}
