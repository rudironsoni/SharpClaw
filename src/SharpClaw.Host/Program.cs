using SharpClaw.Execution.Abstractions;
using SharpClaw.Extensions.Hosting;
using SharpClaw.HttpApi;

var builder = WebApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<SharpClawHealthCheck>("sharpclaw");

builder.AddSharpClawHosting(new ExecutionProviderPolicy(
    DefaultProvider: "dind",
    FallbackProvider: "podman",
    EnabledProviders: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dind",
        "podman",
        "daytona",
        "kubernetes"
    }));

var app = builder.Build();

// Security headers middleware
app.UseHsts();
app.UseXContentTypeOptions();
app.UseReferrerPolicy(opts => opts.NoReferrer());
app.UseXXssProtection(opts => opts.EnabledWithBlockMode());

app.MapSharpClawHttpApiEndpoints();

// Map health check endpoints with configurable path
var healthCheckOptions = builder.Configuration.GetSection("HealthChecks").Get<HealthCheckOptions>() ?? new HealthCheckOptions();
app.MapHealthChecks(healthCheckOptions.Path, new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                exception = e.Value.Exception?.Message,
                duration = e.Value.Duration.ToString()
            })
        };
        await System.Text.Json.JsonSerializer.SerializeAsync(context.Response.Body, response);
    }
});

app.Run();

public partial class Program;
