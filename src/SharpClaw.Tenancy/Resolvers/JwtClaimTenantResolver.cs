using Microsoft.AspNetCore.Http;
using SharpClaw.Tenancy.Abstractions;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace SharpClaw.Tenancy.Resolvers;

/// <summary>
/// Resolves tenant from a JWT claim.
/// </summary>
public sealed class JwtClaimTenantResolver : ITenantResolver
{
    private readonly string _claimType;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtClaimTenantResolver"/> class.
    /// </summary>
    /// <param name="claimType">The claim type to read tenant from. Defaults to "tenant_id".</param>
    public JwtClaimTenantResolver(string claimType = "tenant_id")
    {
        ArgumentException.ThrowIfNullOrEmpty(claimType, nameof(claimType));
        _claimType = claimType;
    }

    /// <inheritdoc />
    public Task<TenantInfo> ResolveAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tenantId = context.User?.FindFirst(_claimType)?.Value;

        if (string.IsNullOrEmpty(tenantId))
        {
            // Fallback to "default" if no tenant claim found
            tenantId = "default";
        }

        return Task.FromResult(new TenantInfo(tenantId, tenantId));
    }
}
