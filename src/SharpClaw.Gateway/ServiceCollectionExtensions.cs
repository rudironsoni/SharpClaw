using Microsoft.Extensions.DependencyInjection;

namespace SharpClaw.Gateway;

/// <summary>
/// Extension methods for registering gateway services.
/// </summary>
public static class GatewayServiceCollectionExtensions
{
    /// <summary>
    /// Adds gateway services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddSharpClawGateway(this IServiceCollection services)
    {
        // Register metrics
        services.AddSingleton<GatewayMetrics>();

        // Register gateway dispatcher
        services.AddSingleton<GatewayDispatcher>();

        // Register connection registry
        services.AddSingleton<ConnectionRegistry>();

        return services;
    }
}
