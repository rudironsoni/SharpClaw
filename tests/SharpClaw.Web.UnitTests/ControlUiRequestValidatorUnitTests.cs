using SharpClaw.Protocol.Contracts;
using SharpClaw.Web;

namespace SharpClaw.Web.UnitTests;

public class ControlUiRequestValidatorUnitTests
{
    [Fact]
    public void ValidateSend_ReturnsInvalidRequest_WhenBodyMissing()
    {
        var error = ControlUiRequestValidator.ValidateSend(null);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error!.Code);
    }

    [Fact]
    public void ValidateSend_ReturnsInvalidRequest_WhenMessageMissing()
    {
        var error = ControlUiRequestValidator.ValidateSend(
            new ControlUiSendRequest(DeviceId: "device-1", Message: ""));

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error!.Code);
    }

    [Fact]
    public void ValidateSend_ReturnsNull_WhenValid()
    {
        var error = ControlUiRequestValidator.ValidateSend(
            new ControlUiSendRequest(DeviceId: "device-1", Message: "hello", IdempotencyKey: "idem-1"));

        Assert.Null(error);
    }

    [Fact]
    public void ValidateAbort_ReturnsInvalidRequest_WhenRunIdMissing()
    {
        var error = ControlUiRequestValidator.ValidateAbort(
            new ControlUiAbortRequest(DeviceId: "device-1", RunId: ""));

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidRequest, error!.Code);
    }

    [Fact]
    public void ValidateAbort_ReturnsNull_WhenValid()
    {
        var error = ControlUiRequestValidator.ValidateAbort(
            new ControlUiAbortRequest(DeviceId: "device-1", RunId: "run-1"));

        Assert.Null(error);
    }
}
