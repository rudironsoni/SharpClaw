using Microsoft.AspNetCore.Http;
using SharpClaw.Tenancy.Abstractions;
using System;
using System.Threading.Tasks;

namespace SharpClaw.Tenancy;

/// <summary>
/// Middleware that resolves the tenant from the HTTP request and sets the async local context.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITenantResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantResolutionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="resolver">The tenant resolver.</param>
    public TenantResolutionMiddleware(RequestDelegate next, ITenantResolver resolver)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var tenantInfo = await _resolver.ResolveAsync(context).ConfigureAwait(false);

            // Set the tenant context for the current async flow
            AsyncLocalTenantContext.Set(tenantInfo.TenantId, tenantInfo.TenantName);

            // Store in HttpContext.Items for later retrieval
            context.Items["TenantContext"] = new AsyncLocalTenantContext();

            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            // Clean up the tenant context
            AsyncLocalTenantContext.Clear();
        }
    }
}
