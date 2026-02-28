using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.Daytona;
using SharpClaw.Execution.Docker;
using SharpClaw.Execution.Kubernetes;
using SharpClaw.Execution.Podman;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.Gateway;
using SharpClaw.Identity;
using SharpClaw.Runs;

namespace SharpClaw.Extensions.Hosting;

public static class HostBuilderExtensions
{
    public static IHostApplicationBuilder AddSharpClawHosting(
        this IHostApplicationBuilder builder,
        ExecutionProviderPolicy? executionProviderPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<ConnectionRegistry>();
        builder.Services.AddSingleton<GatewayHealthService>();
        builder.Services.AddSingleton<RunCoordinator>();
        builder.Services.AddSingleton<IdentityService>();

        builder.Services.AddSingleton<ISandboxProvider, DockerSandboxProvider>();
        builder.Services.AddSingleton<ISandboxProvider, PodmanSandboxProvider>();
        builder.Services.AddSingleton<ISandboxProvider, DaytonaSandboxProvider>();
        builder.Services.AddSingleton<ISandboxProvider, KubernetesSandboxProvider>();

        builder.Services.AddSingleton(executionProviderPolicy ?? new ExecutionProviderPolicy());
        builder.Services.AddSingleton<SandboxManagerService>();

        builder.Services.AddSingleton(provider =>
        {
            var dispatcher = new GatewayDispatcher();
            var runCoordinator = provider.GetRequiredService<RunCoordinator>();
            GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runCoordinator);
            return dispatcher;
        });

        return builder;
    }
}
