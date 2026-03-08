using Microsoft.AspNetCore.Builder;

namespace SharpClaw.Host;

/// <summary>
/// Extension methods for adding security headers to HTTP responses.
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds X-Content-Type-Options: nosniff header.
    /// </summary>
    public static IApplicationBuilder UseXContentTypeOptions(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await next();
        });
    }

    /// <summary>
    /// Adds Referrer-Policy header.
    /// </summary>
    public static IApplicationBuilder UseReferrerPolicy(this IApplicationBuilder app, string policy = "strict-origin-when-cross-origin")
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers["Referrer-Policy"] = policy;
            await next();
        });
    }

    /// <summary>
    /// Adds X-XSS-Protection header.
    /// </summary>
    public static IApplicationBuilder UseXXssProtection(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            await next();
        });
    }
}
