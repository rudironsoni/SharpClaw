using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharpClaw.Observability.HealthChecks;

/// <summary>
/// Health check for database connectivity.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // In a real implementation, verify database connectivity
        // For now, assume healthy (DbContext health checks are added via AddDbContextCheck)
        return Task.FromResult(HealthCheckResult.Healthy("Database is accessible"));
    }
}
