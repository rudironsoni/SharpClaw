using DotNet.Testcontainers.Containers;
using Testcontainers.K3s;
using Xunit;

namespace SharpClaw.TestCommon;

/// <summary>
/// Shared K3s cluster for Kubernetes integration tests (requires Docker + Testcontainers).
/// </summary>
public sealed class K3sClusterFixture : IAsyncLifetime
{
    private const string DefaultImage = "rancher/k3s:v1.30.5-k3s1";
    private readonly K3sContainer _container;

    public K3sClusterFixture()
    {
        var image = Environment.GetEnvironmentVariable("SHARPCLAW_K3S_IMAGE") ?? DefaultImage;

        var builder = new K3sBuilder(image)
            .WithPrivileged(true)
            .WithCommand("server", "--kubelet-arg=feature-gates=KubeletInUserNamespace=true");

        if (File.Exists("/dev/kmsg"))
        {
            builder = builder.WithBindMount("/dev/kmsg", "/dev/kmsg");
        }

        _container = builder.Build();
    }

    public string KubeConfigPath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();

            var kubeConfig = await _container.GetKubeconfigAsync();
            KubeConfigPath = Path.GetTempFileName();
            await File.WriteAllTextAsync(KubeConfigPath, kubeConfig);
        }
        catch
        {
            try
            {
                await DisposeAsync();
            }
            catch
            {
                // Ignore cleanup failures during startup.
            }

            throw;
        }
    }

    public async Task DisposeAsync()
    {
        var errors = new List<Exception>();

        try
        {
            if (!string.IsNullOrWhiteSpace(KubeConfigPath) && File.Exists(KubeConfigPath))
            {
                File.Delete(KubeConfigPath);
            }
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException("Failed to delete K3s kubeconfig file.", ex));
        }

        try
        {
            await _container.StopAsync();
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException("Failed to stop K3s container.", ex));
        }

        try
        {
            await _container.DisposeAsync();
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException("Failed to dispose K3s container.", ex));
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("Failed to dispose K3s test fixture.", errors);
        }
    }
}
