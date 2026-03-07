using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpClaw.RateLimiting.Abstractions;

namespace SharpClaw.RateLimiting.Middleware;

/// <summary>
/// Rate limiting middleware for ASP.NET Core.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _legacyRateLimiter;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimiterOptions _options;
    private readonly RateLimitingFeatureFlags _featureFlags;
    
    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimiter rateLimiter,
        ILogger<RateLimitingMiddleware> logger,
        IOptions<RateLimiterOptions> options,
        RateLimitingFeatureFlags featureFlags)
    {
        _next = next;
        _legacyRateLimiter = rateLimiter;
        _logger = logger;
        _options = options.Value;
        _featureFlags = featureFlags;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for health checks
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }
        
        // Log migration metrics if enabled
        if (_featureFlags.LogMigrationMetrics)
        {
            _logger.LogDebug(
                "Rate limiting mode: {Mode} for {Path}",
                _featureFlags.UseNewRateLimiting ? "System.Threading.RateLimiting" : "Legacy",
                context.Request.Path);
        }
        
        if (_featureFlags.UseNewRateLimiting)
        {
            // Use new System.Threading.RateLimiting via endpoint middleware
            // The actual rate limiting is handled by AddRateLimiter pipeline
            await _next(context);
        }
        else
        {
            // Use legacy rate limiting
            await InvokeLegacyRateLimiting(context);
        }
    }
    
    private async Task InvokeLegacyRateLimiting(HttpContext context)
    {
        var key = GetRateLimitKey(context);
        
        var lease = await _legacyRateLimiter.AcquireAsync(key, 1, context.RequestAborted);
        
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
            // Get tenant ID from HttpContext.Items (set by middleware earlier in pipeline)
            // Falls back to "default" if not explicitly set
            var tenantId = context.Items.TryGetValue("TenantId", out var tenantIdValue) 
                ? tenantIdValue?.ToString() ?? "default"
                : "default";
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
