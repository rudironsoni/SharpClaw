using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SharpClaw.Observability.HealthChecks;
using SharpClaw.Observability.Logging;
using SharpClaw.Observability.Middleware;

namespace SharpClaw.Observability;

/// <summary>
/// Extension methods for adding observability services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds observability services including logging, tracing, metrics, and health checks.
    /// </summary>
    public static IServiceCollection AddSharpClawObservability(
        this IServiceCollection services,
        string serviceName = "SharpClaw",
        string serviceVersion = "1.0.0")
    {
        // Correlation ID tracking
        services.AddSingleton<ICorrelationIdAccessor, AsyncLocalCorrelationIdAccessor>();

        // Configure structured logging
        services.AddLogging(builder =>
        {
            builder.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
                {
                    Indented = false
                };
            });
        });

        // OpenTelemetry Tracing
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName, serviceVersion: serviceVersion))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource("SharpClaw.Gateway")
                    .AddSource("SharpClaw.Runs")
                    .AddSource("SharpClaw.Identity");

                // Export to OTLP (OpenTelemetry Protocol) if endpoint configured
                var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName, serviceVersion: serviceVersion))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("SharpClaw.Metrics")
                    .AddPrometheusExporter();
            });

        // Health Checks
        services.AddHealthChecks()
            .AddCheck<GatewayHealthCheck>("gateway", tags: ["ready"])
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
            .AddCheck<IdentityServiceHealthCheck>("identity", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Adds observability middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseSharpClawObservability(this IApplicationBuilder app)
    {
        // Add correlation ID middleware first
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Add OpenTelemetry Prometheus metrics endpoint
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        // Map health check endpoints
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => true, // All checks for liveness
                ResponseWriter = HealthCheckResponseWriter.WriteResponse
            });

            endpoints.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready"),
                ResponseWriter = HealthCheckResponseWriter.WriteResponse
            });
        });

        return app;
    }
}
