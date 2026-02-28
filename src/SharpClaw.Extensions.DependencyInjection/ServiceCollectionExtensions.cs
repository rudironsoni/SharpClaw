using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.Daytona;
using SharpClaw.Execution.Docker;
using SharpClaw.Execution.Kubernetes;
using SharpClaw.Execution.Podman;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.Gateway;
using SharpClaw.Identity;
using SharpClaw.Runs;

namespace SharpClaw.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharpClawModules(
        this IServiceCollection services,
        ExecutionProviderPolicy? executionProviderPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ConnectionRegistry>();
        services.AddSingleton<GatewayHealthService>();
        services.AddSingleton<RunCoordinator>();
        services.AddSingleton<IdentityService>();

        services.AddSingleton<ISandboxProvider, DockerSandboxProvider>();
        services.AddSingleton<ISandboxProvider, PodmanSandboxProvider>();
        services.AddSingleton<ISandboxProvider, DaytonaSandboxProvider>();
        services.AddSingleton<ISandboxProvider, KubernetesSandboxProvider>();

        services.AddSingleton(executionProviderPolicy ?? new ExecutionProviderPolicy());
        services.AddSingleton<SandboxManagerService>();

        services.AddSingleton(provider =>
        {
            var dispatcher = new GatewayDispatcher();
            var runCoordinator = provider.GetRequiredService<RunCoordinator>();
            GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runCoordinator);
            return dispatcher;
        });

        return services;
    }
}
