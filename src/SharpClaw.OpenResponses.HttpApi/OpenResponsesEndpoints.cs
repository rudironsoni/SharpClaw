using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SharpClaw.Protocol.Contracts;
using SharpClaw.Runs;

namespace SharpClaw.OpenResponses.HttpApi;

public sealed record OpenResponsesRequest(
    string? Model,
    object? Input,
    bool? Stream = null,
    string? User = null);

public sealed record OpenResponsesResponse(
    string Id,
    string Object,
    string Status,
    string OutputText);

public static class OpenResponsesValidator
{
    public static ErrorShape? Validate(OpenResponsesRequest? request)
    {
        if (request is null)
        {
            return new ErrorShape(ErrorCodes.InvalidRequest, "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return new ErrorShape(ErrorCodes.InvalidRequest, "Model is required.");
        }

        if (request.Input is null)
        {
            return new ErrorShape(ErrorCodes.InvalidRequest, "Input is required.");
        }

        return null;
    }
}

public static class OpenResponsesEndpoints
{
    public static WebApplication MapOpenResponsesEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/responses", HandleResponseAsync);
        return app;
    }

    private static async Task<IResult> HandleResponseAsync(
        OpenResponsesRequest request,
        [FromServices] RunCoordinator runs,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = OpenResponsesValidator.Validate(request);
        if (validation is not null)
        {
            return Results.BadRequest(validation);
        }

        var inputText = JsonSerializer.Serialize(request.Input);
        var start = await runs.StartAsync(inputText, "default", idempotencyKey: null, cancellationToken);

        if (request.Stream == true)
        {
            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.ContentType = "text/event-stream";

            await WriteSseAsync(httpContext, "response.created", new { id = start.RunId, status = "in_progress" }, cancellationToken);

            await foreach (var evt in runs.ReadEventsAsync(start.RunId, cancellationToken).ConfigureAwait(false))
            {
                switch (evt.Event)
                {
                    case "run.running":
                        await WriteSseAsync(httpContext, "response.in_progress", new { id = start.RunId }, cancellationToken);
                        break;
                    case "run.delta":
                        await WriteSseAsync(httpContext, "response.output_text.delta", new { id = start.RunId, delta = evt.Data ?? string.Empty }, cancellationToken);
                        break;
                    case "run.completed":
                        await WriteSseAsync(httpContext, "response.completed", new { id = start.RunId, status = "completed" }, cancellationToken);
                        return TypedResults.Empty;
                    case "run.aborted":
                        await WriteSseAsync(httpContext, "response.completed", new { id = start.RunId, status = "cancelled" }, cancellationToken);
                        return TypedResults.Empty;
                    case "run.failed":
                        await WriteSseAsync(httpContext, "response.completed", new { id = start.RunId, status = "failed", error = evt.Data }, cancellationToken);
                        return TypedResults.Empty;
                }
            }

            await WriteSseAsync(httpContext, "response.completed", new { id = start.RunId, status = "completed" }, cancellationToken);

            return TypedResults.Empty;
        }

        return Results.Ok(new OpenResponsesResponse(
            Id: start.RunId,
            Object: "response",
            Status: "completed",
            OutputText: "ok"));
    }

    private static async Task WriteSseAsync(HttpContext context, string evt, object payload, CancellationToken cancellationToken)
    {
        var writer = context.Response.BodyWriter;
        var json = JsonSerializer.Serialize(payload);
        var sse = $"event: {evt}\ndata: {json}\n\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(sse);
        await writer.WriteAsync(bytes, cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }
}
