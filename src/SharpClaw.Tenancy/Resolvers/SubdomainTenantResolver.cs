using Microsoft.AspNetCore.Http;
using SharpClaw.Tenancy.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClaw.Tenancy.Resolvers;

/// <summary>
/// Resolves tenant from the request subdomain (e.g., tenant.example.com).
/// </summary>
public sealed class SubdomainTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    public Task<TenantInfo> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.Request.Host.Host;
        var parts = host.Split('.');

        // If subdomain exists (tenant.example.com), use it; otherwise use "default"
        var tenantId = parts.Length > 2 ? parts[0] : "default";
        var tenantName = tenantId;

        return Task.FromResult(new TenantInfo(tenantId, tenantName));
    }
}
