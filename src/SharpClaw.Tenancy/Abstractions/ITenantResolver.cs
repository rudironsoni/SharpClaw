using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClaw.Tenancy.Abstractions;

/// <summary>
/// Resolves the tenant from an HTTP request context.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Resolves the tenant from the HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved tenant information.</returns>
    Task<TenantInfo> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default);
}
