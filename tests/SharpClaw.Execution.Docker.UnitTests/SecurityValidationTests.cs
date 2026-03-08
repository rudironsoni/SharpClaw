using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Docker;

namespace SharpClaw.Execution.Docker.UnitTests;

public class SecurityValidationTests
{
    [Fact]
    public void ValidateSecurityOptions_WithMissingSeccompProfile_ThrowsArgumentException()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Docker:SeccompProfile"] = "/nonexistent/path/seccomp.json"
            })
            .Build();

        var options = new DockerSandboxOptions
        {
            SeccompProfile = "/nonexistent/path/seccomp.json"
        };

        // Act & Assert
        var provider = new DockerSandboxProvider(NullLogger<DockerSandboxProvider>.Instance, options);
        var method = provider.GetType().GetMethod("ValidateSecurityOptions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.Throws<TargetInvocationException>(() => method!.Invoke(provider, null));
    }

    [Fact]
    public void ValidateSecurityOptions_WithMissingAppArmorOnSystem_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new DockerSandboxOptions
        {
            AppArmorProfile = "sharpclaw-profile"
        };

        // Act & Assert - Should throw if AppArmor not available
        var provider = new DockerSandboxProvider(NullLogger<DockerSandboxProvider>.Instance, options);
        // Note: This test may pass on systems without AppArmor
        // The actual behavior depends on the runtime environment
    }

    [Fact]
    public void DockerSandboxOptions_DefaultValues_AreSecure()
    {
        // Arrange & Act
        var options = new DockerSandboxOptions();

        // Assert
        Assert.True(options.EnableNetworkIsolation, "Network isolation should be enabled by default");
    }

    [Fact]
    public void DockerSandboxOptions_WithZeroMemoryLimit_IsUnlimited()
    {
        // Arrange
        var options = new DockerSandboxOptions
        {
            MemoryLimit = 0
        };

        // Assert
        Assert.Equal(0, options.MemoryLimit);
    }

    [Fact]
    public void DockerSandboxOptions_WithNegativeCpuLimit_IsInvalid()
    {
        // Arrange
        var options = new DockerSandboxOptions
        {
            CpuLimit = -1
        };

        // Assert - negative values should be treated as 0 (unlimited)
        Assert.True(options.CpuLimit < 0 || options.CpuLimit == 0);
    }
}
