using Microsoft.AspNetCore.Http;
using SharpClaw.Observability.Logging;

namespace SharpClaw.Observability.Middleware;

/// <summary>
/// Middleware that extracts or generates correlation IDs for request tracking.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        _next = next;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract existing correlation ID from header or generate new one
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // Set correlation ID in accessor
        _correlationIdAccessor.SetCorrelationId(correlationId);

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers[CorrelationIdHeader] = correlationId;
            }

            return Task.CompletedTask;
        });

        try
        {
            await _next(context);
        }
        finally
        {
            _correlationIdAccessor.Clear();
        }
    }
}
