using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.RateLimiting.Abstractions;
using SharpClaw.RateLimiting.Stores;
using SharpClaw.RateLimiting.Strategies;

namespace SharpClaw.RateLimiting;

/// <summary>
/// Extension methods for registering rate limiting services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds rate limiting services to the DI container.
    /// </summary>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        Action<RateLimiterOptions>? configureOptions = null)
    {
        services.AddOptions<RateLimiterOptions>();
        
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        
        services.AddSingleton<IRateLimitStore, MemoryRateLimitStore>();
        services.AddSingleton<IRateLimiter, TokenBucketRateLimiter>();
        
        return services;
    }
    
    /// <summary>
    /// Adds rate limiting services with custom store.
    /// </summary>
    public static IServiceCollection AddRateLimiting<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
        this IServiceCollection services,
        Action<RateLimiterOptions>? configureOptions = null)
        where TStore : class, IRateLimitStore
    {
        services.AddOptions<RateLimiterOptions>();
        
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        
        services.AddSingleton<IRateLimitStore, TStore>();
        services.AddSingleton<IRateLimiter, TokenBucketRateLimiter>();
        
        return services;
    }
}
