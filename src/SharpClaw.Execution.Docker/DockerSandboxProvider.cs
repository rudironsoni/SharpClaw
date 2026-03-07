using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using SharpClaw.Execution.Abstractions;
using System.IO;

namespace SharpClaw.Execution.Docker;

/// <summary>
/// Docker-based sandbox provider using Docker.DotNet for container lifecycle management.
/// </summary>
public sealed class DockerSandboxProvider : ISandboxProvider, IDisposable
{
    private readonly DockerClient _client;
    private readonly ILogger<DockerSandboxProvider> _logger;
    private readonly DockerSandboxOptions _options;

    public string Name => "dind";

    public DockerSandboxProvider(
        ILogger<DockerSandboxProvider> logger,
        DockerSandboxOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new DockerSandboxOptions();

        // Connect to Docker daemon via Unix socket or TCP
        var dockerUri = _options.DockerUri ?? "unix:///var/run/docker.sock";
        _client = new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();

        _logger.LogInformation("Docker sandbox provider initialized with URI: {Uri}", dockerUri);
    }

    private void ValidateSecurityOptions()
    {
        // Validate Seccomp profile path if specified
        if (!string.IsNullOrEmpty(_options.SeccompProfile))
        {
            if (!File.Exists(_options.SeccompProfile))
            {
                throw new ArgumentException(
                    $"Seccomp profile file not found: {_options.SeccompProfile}. " +
                    "Ensure the file exists and the path is correct.",
                    nameof(_options.SeccompProfile));
            }

            // Validate file is readable
            try
            {
                File.OpenRead(_options.SeccompProfile).Dispose();
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Seccomp profile file is not readable: {_options.SeccompProfile}",
                    nameof(_options.SeccompProfile),
                    ex);
            }

            _logger.LogInformation("Using seccomp profile: {Profile}", _options.SeccompProfile);
        }

        // Validate AppArmor profile if specified
        if (!string.IsNullOrEmpty(_options.AppArmorProfile))
        {
            // Check if AppArmor is enabled on the system
            var appArmorPath = "/sys/kernel/security/apparmor";
            if (!Directory.Exists(appArmorPath))
            {
                throw new InvalidOperationException(
                    "AppArmor is not enabled on this system. " +
                    "Cannot use AppArmor profiles without kernel support.");
            }

            _logger.LogInformation("Using AppArmor profile: {Profile}", _options.AppArmorProfile);
        }
    }

    public async Task<SandboxHandle> StartAsync(CancellationToken cancellationToken = default)
    {
        var containerName = $"sharpclaw-{Guid.NewGuid():N}";

        _logger.LogInformation("Creating Docker container: {ContainerName}", containerName);

        var image = _options.ContainerImage ?? "alpine:latest";
        try
        {
            await _client.Images.InspectImageAsync(image, cancellationToken);
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Image {Image} not found locally, pulling...", image);

            var separatorIdx = image.LastIndexOf(':');
            var imageName = separatorIdx > 0 ? image.Substring(0, separatorIdx) : image;
            var tag = separatorIdx > 0 ? image.Substring(separatorIdx + 1) : "latest";

            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = imageName, Tag = tag },
                null,
                new Progress<JSONMessage>(),
                cancellationToken);

            _logger.LogInformation("Image {Image} pulled successfully", image);
        }

        // Validate security options before applying
        ValidateSecurityOptions();

        var securityOpts = new List<string> { "no-new-privileges:true" };
        if (!string.IsNullOrEmpty(_options.SeccompProfile))
        {
            securityOpts.Add($"seccomp={_options.SeccompProfile}");
        }

        if (!string.IsNullOrEmpty(_options.AppArmorProfile))
        {
            securityOpts.Add($"apparmor={_options.AppArmorProfile}");
        }

        // Configure container with security hardening
        var hostConfig = new HostConfig
        {
            // Network isolation
            NetworkMode = _options.EnableNetworkIsolation ? "none" : "bridge",

            // Security: Drop all capabilities
            CapDrop = new List<string> { "ALL" },

            // Security: Add only required capabilities
            CapAdd = new List<string>(new[]
            {
                "CHOWN",
                "SETGID",
                "SETUID"
            }),

            // Security: No new privileges and custom profiles
            SecurityOpt = securityOpts,

            // Security: Read-only root filesystem
            ReadonlyRootfs = true,

            // Resource limits
            Memory = _options.MemoryLimit > 0 ? _options.MemoryLimit * 1024 * 1024 : 0, // Convert MB to bytes
            NanoCPUs = _options.CpuLimit > 0 ? _options.CpuLimit * 1_000_000_000 : 0, // Convert cores to nano CPUs

            // Writable tmpfs mounts
            Tmpfs = new Dictionary<string, string>
            {
                { "/tmp", "rw,noexec,nosuid,size=100m" },
                { "/var/tmp", "rw,noexec,nosuid,size=50m" }
            }
        };

        // Create the container with new API (CreateContainerParameters takes properties directly)
        var createParams = new CreateContainerParameters
        {
            Image = _options.ContainerImage ?? "alpine:latest",
            Cmd = new List<string> { "sleep", "infinity" }, // Keep container running
            Hostname = containerName,
            Labels = new Dictionary<string, string>
            {
                { "sharpclaw.provider", Name },
                { "sharpclaw.created", DateTimeOffset.UtcNow.ToString("O") },
                { "sharpclaw.managed", "true" }
            },
            Env = new List<string>
            {
                $"SHARPCLAW_CONTAINER_NAME={containerName}",
                $"SHARPCLAW_PROVIDER={Name}"
            },
            HostConfig = hostConfig,
            Name = containerName
        };

        var response = await _client.Containers.CreateContainerAsync(createParams, cancellationToken);
        var containerId = response.ID;

        _logger.LogInformation(
            "Container {ContainerName} created with ID {ContainerId}",
            containerName, containerId.Substring(0, 12));

        // Start the container
        await _client.Containers.StartContainerAsync(
            containerId,
            new ContainerStartParameters(),
            cancellationToken);

        _logger.LogInformation("Container {ContainerName} started", containerName);

        return new SandboxHandle(Name, $"dind-{containerId}");
    }

    public async Task StopAsync(SandboxHandle handle, CancellationToken cancellationToken = default)
    {
        if (handle.Provider != Name)
        {
            throw new InvalidOperationException(
                $"Provider mismatch: expected {Name}, got {handle.Provider}");
        }

        var containerId = handle.SandboxId.StartsWith("dind-", StringComparison.Ordinal)
            ? handle.SandboxId.Substring(5)
            : handle.SandboxId;

        _logger.LogInformation("Stopping Docker container: {ContainerId}", containerId.Substring(0, Math.Min(12, containerId.Length)));

        try
        {
            // Stop the container with timeout
            await _client.Containers.StopContainerAsync(
                containerId,
                new ContainerStopParameters
                {
                    WaitBeforeKillSeconds = (uint)_options.StopTimeoutSeconds
                },
                cancellationToken);

            _logger.LogInformation("Container {ContainerId} stopped", containerId.Substring(0, Math.Min(12, containerId.Length)));
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Container {ContainerId} not found during stop", containerId.Substring(0, Math.Min(12, containerId.Length)));
        }

        try
        {
            // Remove the container
            await _client.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters
                {
                    Force = true, // Force removal even if running
                    RemoveVolumes = true
                },
                cancellationToken);

            _logger.LogInformation("Container {ContainerId} removed", containerId.Substring(0, Math.Min(12, containerId.Length)));
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Container {ContainerId} not found during removal", containerId.Substring(0, Math.Min(12, containerId.Length)));
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

/// <summary>
/// Docker sandbox configuration options.
/// </summary>
public sealed record DockerSandboxOptions
{
    /// <summary>
    /// Docker daemon URI (e.g., unix:///var/run/docker.sock or tcp://localhost:2375).
    /// </summary>
    public string? DockerUri { get; init; }

    /// <summary>
    /// Container image to use.
    /// </summary>
    public string? ContainerImage { get; init; } = "alpine:latest";

    /// <summary>
    /// Enable network isolation (disables network access).
    /// </summary>
    public bool EnableNetworkIsolation { get; init; } = true;

    /// <summary>
    /// Memory limit in MB.
    /// </summary>
    public long MemoryLimit { get; init; } = 512; // 512 MB

    /// <summary>
    /// CPU limit in cores (e.g., 1.0 = 1 core, 0.5 = half core).
    /// </summary>
    public long CpuLimit { get; init; } = 1;

    /// <summary>
    /// Timeout in seconds before forcefully killing a container.
    /// </summary>
    public int StopTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Seccomp profile to use. Default is null (Docker's default profile).
    /// </summary>
    public string? SeccompProfile { get; init; }

    /// <summary>
    /// AppArmor profile to use. Default is null (Docker's default profile).
    /// </summary>
    public string? AppArmorProfile { get; init; }
}
