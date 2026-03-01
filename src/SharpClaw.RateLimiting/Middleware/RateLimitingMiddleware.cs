using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpClaw.RateLimiting.Abstractions;
using SharpClaw.Tenancy;

namespace SharpClaw.RateLimiting.Middleware;

/// <summary>
/// Rate limiting middleware for ASP.NET Core.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimiterOptions _options;
    
    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimiter rateLimiter,
        ILogger<RateLimitingMiddleware> logger,
        IOptions<RateLimiterOptions> options)
    {
        _next = next;
        _rateLimiter = rateLimiter;
        _logger = logger;
        _options = options.Value;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for health checks
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }
        
        // Get key based on device ID or IP address
        var key = GetRateLimitKey(context);
        
        var lease = await _rateLimiter.AcquireAsync(key, 1, context.RequestAborted);
        
        if (!lease.IsAcquired)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {Key} on {Path}",
                key, context.Request.Path);
            
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Append("Retry-After", GetRetryAfterSeconds(lease).ToString());
            
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }
        
        try
        {
            await _next(context);
        }
        finally
        {
            lease.Dispose();
        }
    }
    
    private string GetRateLimitKey(HttpContext context)
    {
        // Try to get device ID from claims or tenant context
        var deviceId = context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(deviceId))
        {
            var tenantId = AsyncLocalTenantContext.Current?.TenantId ?? "default";
            return $"{tenantId}:{deviceId}";
        }
        
        // Fall back to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }
    
    private static int GetRetryAfterSeconds(RateLimitLease lease)
    {
        if (lease.RetryAfter is TimeSpan retryAfter)
        {
            return (int)Math.Ceiling(retryAfter.TotalSeconds);
        }
        
        return 60; // Default 1 minute
    }
}
