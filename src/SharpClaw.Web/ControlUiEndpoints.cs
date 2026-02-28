using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SharpClaw.Gateway;
using SharpClaw.Identity;
using SharpClaw.Protocol.Contracts;
using SharpClaw.Runs;

namespace SharpClaw.Web;

public sealed record ControlUiSendRequest(
    string DeviceId,
    string Message,
    string? IdempotencyKey = null,
    IReadOnlyList<string>? Scopes = null);

public sealed record ControlUiAbortRequest(
    string DeviceId,
    string RunId,
    IReadOnlyList<string>? Scopes = null);

public static class ControlUiRequestValidator
{
    public static ErrorShape? ValidateSend(ControlUiSendRequest? request)
    {
        if (request is null)
        {
            return new ErrorShape(ErrorCodes.InvalidRequest, "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return new ErrorShape(ErrorCodes.InvalidRequest, "DeviceId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new ErrorShape(ErrorCodes.InvalidRequest, "Message is required.");
        }

        return null;
    }

    public static ErrorShape? ValidateAbort(ControlUiAbortRequest? request)
    {
        if (request is null)
        {
            return new ErrorShape(ErrorCodes.InvalidRequest, "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return new ErrorShape(ErrorCodes.InvalidRequest, "DeviceId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RunId))
        {
            return new ErrorShape(ErrorCodes.InvalidRequest, "RunId is required.");
        }

        return null;
    }
}

public static class ControlUiEndpoints
{
    private static readonly string[] DefaultWriteScopes = [ScopeRequirements.OperatorWrite];

    public static WebApplication MapControlUiEndpoints(this WebApplication app)
    {
        app.MapGet("/control-ui/state", ([FromServices] ConnectionRegistry connections) =>
            Results.Ok(new { activeConnections = connections.ActiveCount }));

        app.MapPost("/control-ui/chat/send", HandleSendAsync);
        app.MapPost("/control-ui/chat/abort", HandleAbortAsync);

        return app;
    }

    private static async Task<IResult> HandleSendAsync(
        ControlUiSendRequest request,
        [FromServices] IdentityService identity,
        [FromServices] GatewayDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var validation = ControlUiRequestValidator.ValidateSend(request);
        if (validation is not null)
        {
            return Results.BadRequest(validation);
        }

        var scopes = request.Scopes?.Count > 0
            ? request.Scopes
            : DefaultWriteScopes;

        var auth = new AuthContext(request.DeviceId, scopes.ToHashSet(StringComparer.OrdinalIgnoreCase), IsPaired: true);
        var authResult = identity.Authorize(auth, "chat.send");
        if (!authResult.Succeeded)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var frame = new RequestFrame(
            Id: $"ui-{Guid.NewGuid():N}",
            Method: "chat.send",
            Payload: new { message = request.Message },
            IdempotencyKey: request.IdempotencyKey);

        var response = await dispatcher.DispatchAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!response.Ok)
        {
            return Results.BadRequest(response.Error);
        }

        return Results.Ok(new
        {
            response.Id,
            response.Ok,
            runId = ExtractString(response.Payload, "runId"),
            status = ExtractString(response.Payload, "status")
        });
    }

    private static async Task<IResult> HandleAbortAsync(
        ControlUiAbortRequest request,
        [FromServices] IdentityService identity,
        [FromServices] GatewayDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var validation = ControlUiRequestValidator.ValidateAbort(request);
        if (validation is not null)
        {
            return Results.BadRequest(validation);
        }

        var scopes = request.Scopes?.Count > 0
            ? request.Scopes
            : DefaultWriteScopes;

        var auth = new AuthContext(request.DeviceId, scopes.ToHashSet(StringComparer.OrdinalIgnoreCase), IsPaired: true);
        var authResult = identity.Authorize(auth, "chat.abort");
        if (!authResult.Succeeded)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var frame = new RequestFrame(
            Id: $"ui-{Guid.NewGuid():N}",
            Method: "chat.abort",
            Payload: new { runId = request.RunId });

        var response = await dispatcher.DispatchAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!response.Ok)
        {
            return Results.BadRequest(response.Error);
        }

        return Results.Ok(new
        {
            response.Id,
            response.Ok,
            runId = ExtractString(response.Payload, "runId"),
            status = ExtractString(response.Payload, "status")
        });
    }

    private static string? ExtractString(object? payload, string property)
    {
        if (payload is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
