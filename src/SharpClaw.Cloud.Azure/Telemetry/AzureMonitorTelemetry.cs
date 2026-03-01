using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SharpClaw.Cloud.Azure.Telemetry;

/// <summary>
/// Azure Monitor telemetry exporter for OpenTelemetry.
/// </summary>
public sealed class AzureMonitorTelemetry : IDisposable
{
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureMonitorTelemetry"/> class.
    /// </summary>
    /// <param name="connectionString">The Application Insights connection string.</param>
    /// <param name="serviceName">The service name.</param>
    /// <param name="serviceVersion">The service version.</param>
    /// <exception cref="ArgumentException">Thrown when connection string is invalid.</exception>
    public AzureMonitorTelemetry(
        string connectionString,
        string serviceName,
        string serviceVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(serviceName);

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: serviceVersion);

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri("https://westus-0.in.applicationinsights.azure.com/v1/logs");
                });
            });
        });

        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(serviceName)
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("https://westus-0.in.applicationinsights.azure.com/v1/traces");
            })
            .Build();

        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("https://westus-0.in.applicationinsights.azure.com/v1/metrics");
            })
            .Build();
    }

    /// <summary>
    /// Gets the tracer for the specified name.
    /// </summary>
    /// <param name="name">The tracer name.</param>
    /// <returns>The tracer instance.</returns>
    public Tracer GetTracer(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tracerProvider.GetTracer(name);
    }

    /// <summary>
    /// Gets the meter for the specified name.
    /// </summary>
    /// <param name="name">The meter name.</param>
    /// <returns>The meter instance.</returns>
    public System.Diagnostics.Metrics.Meter GetMeter(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new System.Diagnostics.Metrics.Meter(name);
    }

    /// <summary>
    /// Creates a logger for the specified category.
    /// </summary>
    /// <param name="categoryName">The logger category name.</param>
    /// <returns>The logger instance.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _loggerFactory.CreateLogger(categoryName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _tracerProvider.Dispose();
            _meterProvider.Dispose();
            _loggerFactory.Dispose();
            _disposed = true;
        }
    }
}
