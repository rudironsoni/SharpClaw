using SharpClaw.Execution.Abstractions;
using SharpClaw.Extensions.Hosting;
using SharpClaw.HttpApi;

var builder = WebApplication.CreateBuilder(args);

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

app.MapSharpClawHttpApiEndpoints();

app.Run();

public partial class Program;
