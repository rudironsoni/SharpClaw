using System.Diagnostics;
using Xunit;

namespace SharpClaw.TestCommon;

/// <summary>
/// Skip tests when Kubernetes (via Docker/Testcontainers) is not available.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class KubernetesAvailableAttribute : FactAttribute
{
    private static readonly Lazy<(bool available, string? reason)> KubernetesStatus = new(() =>
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
                return (false, "Docker daemon is not running (required for Kubernetes tests)");
            }

            // Check if rancher/k3s image exists locally
            var imagesProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "images rancher/k3s --format '{{.Repository}}'",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            imagesProcess.Start();
            imagesProcess.WaitForExit(5000);
            var output = imagesProcess.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrEmpty(output))
            {
                return (false, "K3s image not available (requires 'docker pull rancher/k3s:v1.30.5-k3s1')");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Kubernetes check failed: {ex.Message}");
        }
    });

    public KubernetesAvailableAttribute()
    {
        var status = KubernetesStatus.Value;
        if (!status.available)
        {
            Skip = status.reason ?? "Kubernetes is not available";
        }
    }
}
