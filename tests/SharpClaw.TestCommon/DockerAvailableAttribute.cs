using System.Diagnostics;
using Xunit;

namespace SharpClaw.TestCommon;

/// <summary>
/// Skip tests when Docker is not available or images cannot be pulled.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class DockerAvailableAttribute : FactAttribute
{
    private static readonly Lazy<(bool available, string? reason)> DockerStatus = new(() =>
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
                return (false, "Docker daemon is not running");
            }

            // Check if alpine:latest image exists locally
            var imagesProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "images alpine:latest --format '{{.Repository}}'",
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
                // Try to pull the image
                var pullProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = "pull alpine:latest",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                pullProcess.Start();
                pullProcess.WaitForExit(30000);
                if (pullProcess.ExitCode != 0)
                {
                    return (false, "Docker alpine:latest image not available and could not be pulled");
                }
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Docker check failed: {ex.Message}");
        }
    });

    public DockerAvailableAttribute()
    {
        var status = DockerStatus.Value;
        if (!status.available)
        {
            Skip = status.reason ?? "Docker is not available";
        }
    }
}
