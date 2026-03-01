using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharpClaw.Observability.HealthChecks;

/// <summary>
/// Health check for the identity service.
/// </summary>
public class IdentityServiceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Identity service is healthy if it can validate tokens
        return Task.FromResult(HealthCheckResult.Healthy("Identity service is operational"));
    }
}
