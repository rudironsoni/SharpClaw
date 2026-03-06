using Xunit;
using System.Diagnostics;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

/// <summary>
/// Integration test fixture for Daytona using Docker Compose.
/// </summary>
public sealed class DaytonaIntegrationTestFixture : IAsyncLifetime
{
    private readonly string _composeFile;
    private readonly string _projectName;
    private bool _disposed;

    public DaytonaIntegrationTestFixture()
    {
        _composeFile = Path.Combine(
            AppContext.BaseDirectory,
            "docker-compose.yaml");

        if (!File.Exists(_composeFile))
        {
            throw new FileNotFoundException(
                "docker-compose.yaml not found.",
                _composeFile);
        }

        _projectName = $"daytona-test-{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        Console.WriteLine("[Daytona] Starting Docker Compose...");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose -f \"{_composeFile}\" -p {_projectName} up -d",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Docker Compose failed: {error}");
        }

        await Task.Delay(TimeSpan.FromSeconds(30));
        Console.WriteLine("[Daytona] Stack started");
    }

    public async Task DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.WriteLine("[Daytona] Stopping...");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"compose -f \"{_composeFile}\" -p {_projectName} down -v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.WaitForExitAsync();
        Console.WriteLine("[Daytona] Stopped");
    }

    public string GetApiBaseUrl() => "http://localhost:3000";
}
