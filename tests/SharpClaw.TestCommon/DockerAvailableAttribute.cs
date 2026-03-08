using System.Diagnostics;
using Xunit;

namespace SharpClaw.TestCommon;

/// <summary>
/// Skip tests when Docker is not available or images cannot be pulled.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class DockerAvailableAttribute : FactAttribute
{
    private static readonly Lazy<bool> IsDockerAvailable = new(() =>
    {
        try
        {
            var process = new Process
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
            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    });

    public DockerAvailableAttribute()
    {
        if (!IsDockerAvailable.Value)
        {
            Skip = "Docker is not available";
        }
    }
}
