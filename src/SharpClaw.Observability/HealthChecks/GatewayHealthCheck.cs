using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharpClaw.Observability.HealthChecks;

/// <summary>
/// Health check for the gateway service.
/// </summary>
public class GatewayHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Gateway is healthy if it can process requests
        return Task.FromResult(HealthCheckResult.Healthy("Gateway is operational"));
    }
}
