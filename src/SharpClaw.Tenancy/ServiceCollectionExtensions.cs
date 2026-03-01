using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Tenancy.Abstractions;
using SharpClaw.Tenancy.Resolvers;

namespace SharpClaw.Tenancy;

/// <summary>
/// Extension methods for registering tenancy services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds tenancy services with subdomain-based resolution.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSubdomainTenancy(this IServiceCollection services)
    {
        services.AddSingleton<ITenantResolver, SubdomainTenantResolver>();
        services.AddSingleton<ITenantContext, AsyncLocalTenantContext>();
        return services;
    }

    /// <summary>
    /// Adds tenancy services with header-based resolution.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="headerName">The header name to read tenant from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHeaderTenancy(this IServiceCollection services, string headerName = "X-Tenant-Id")
    {
        services.AddSingleton<ITenantResolver>(_ => new HeaderTenantResolver(headerName));
        services.AddSingleton<ITenantContext, AsyncLocalTenantContext>();
        return services;
    }

    /// <summary>
    /// Adds tenancy services with JWT claim-based resolution.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="claimType">The claim type to read tenant from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJwtClaimTenancy(this IServiceCollection services, string claimType = "tenant_id")
    {
        services.AddSingleton<ITenantResolver>(_ => new JwtClaimTenantResolver(claimType));
        services.AddSingleton<ITenantContext, AsyncLocalTenantContext>();
        return services;
    }

    /// <summary>
    /// Adds tenancy services with a custom resolver.
    /// </summary>
    /// <typeparam name="TResolver">The type of the tenant resolver.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTenancy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResolver>(this IServiceCollection services)
        where TResolver : class, ITenantResolver
    {
        services.AddSingleton<ITenantResolver, TResolver>();
        services.AddSingleton<ITenantContext, AsyncLocalTenantContext>();
        return services;
    }
}
