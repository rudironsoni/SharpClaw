using Microsoft.AspNetCore.Http;
using SharpClaw.Tenancy.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClaw.Tenancy.Resolvers;

/// <summary>
/// Resolves tenant from a custom HTTP header.
/// </summary>
public sealed class HeaderTenantResolver : ITenantResolver
{
    private readonly string _headerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderTenantResolver"/> class.
    /// </summary>
    /// <param name="headerName">The header name to read tenant from. Defaults to "X-Tenant-Id".</param>
    public HeaderTenantResolver(string headerName = "X-Tenant-Id")
    {
        ArgumentException.ThrowIfNullOrEmpty(headerName, nameof(headerName));
        _headerName = headerName;
    }

    /// <inheritdoc />
    public Task<TenantInfo> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenantId = context.Request.Headers[_headerName].FirstOrDefault();

        if (string.IsNullOrEmpty(tenantId))
        {
            tenantId = "default";
        }

        return Task.FromResult(new TenantInfo(tenantId, tenantId));
    }
}
