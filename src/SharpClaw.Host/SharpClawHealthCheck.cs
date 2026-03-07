using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SharpClaw.Host;

/// <summary>
/// Health check for SharpClaw gateway.
/// </summary>
public sealed class SharpClawHealthCheck : IHealthCheck
{
    private readonly ILogger<SharpClawHealthCheck> _logger;

    public SharpClawHealthCheck(ILogger<SharpClawHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Health check executed");
            return Task.FromResult(HealthCheckResult.Healthy("SharpClaw is healthy"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("SharpClaw health check failed", ex));
        }
    }
}
