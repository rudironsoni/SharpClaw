using System.Diagnostics;
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
    private readonly K3sContainer? _container;
    private bool _isAvailable;

    public K3sClusterFixture()
    {
        _isAvailable = IsKubernetesAvailable();
        if (!_isAvailable)
        {
            return;
        }

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
    public bool IsAvailable => _isAvailable;

    private static bool IsKubernetesAvailable()
    {
        try
        {
            // Check if Docker daemon is running
            var versionProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "version --format '{{.Server.Version}}'",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            versionProcess.Start();
            versionProcess.WaitForExit(5000);
            if (versionProcess.ExitCode != 0)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task InitializeAsync()
    {
        if (!_isAvailable || _container == null)
        {
            return;
        }

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

            // Mark as unavailable so tests can skip
            _isAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (!_isAvailable || _container == null)
        {
            return;
        }

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
