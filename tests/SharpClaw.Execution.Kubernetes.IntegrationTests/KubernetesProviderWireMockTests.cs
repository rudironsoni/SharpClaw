using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Kubernetes;
using SharpClaw.Execution.SandboxManager;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SharpClaw.Execution.Kubernetes.IntegrationTests;

public class KubernetesProviderWireMockTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly SandboxManagerService _manager;
    private readonly string _kubeConfigPath;

    public KubernetesProviderWireMockTests()
    {
        _server = WireMockServer.Start();
        
        var kubeConfigYaml = $@"
apiVersion: v1
clusters:
- cluster:
    server: {_server.Urls[0]}
    insecure-skip-tls-verify: true
  name: test-cluster
contexts:
- context:
    cluster: test-cluster
    user: test-user
  name: test-context
current-context: test-context
kind: Config
preferences: {{}}
users:
- name: test-user
  user:
    token: fake-token
";
        _kubeConfigPath = Path.GetTempFileName();
        File.WriteAllText(_kubeConfigPath, kubeConfigYaml);

        var provider = new KubernetesSandboxProvider(
            NullLogger<KubernetesSandboxProvider>.Instance,
            kubeConfigPath: _kubeConfigPath,
            @namespace: "default");

        _manager = new SandboxManagerService([provider], Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpClaw.Execution.SandboxManager.SandboxManagerService>.Instance, defaultProvider: "kubernetes");
    }

    [Fact]
    public async Task StartDefaultAsync_UsesWireMockCluster()
    {
        var pendingPodJson = @"{ ""metadata"": { ""name"": ""mock-pod-123"" }, ""status"": { ""phase"": ""Pending"" } }";
        var runningPodJson = @"{ ""metadata"": { ""name"": ""mock-pod-123"" }, ""status"": { ""phase"": ""Running"" } }";

        // Arrange
        // Mock pod creation
        _server.Given(
            Request.Create().WithPath("/api/v1/namespaces/default/pods").UsingPost()
        ).RespondWith(
            Response.Create().WithStatusCode(201).WithHeader("Content-Type", "application/json").WithBody(pendingPodJson)
        );

        // Mock pod read (wait for running)
        _server.Given(
            Request.Create().WithPath(p => p != null && p.StartsWith("/api/v1/namespaces/default/pods/") && !p.EndsWith("/status")).UsingGet()
        ).RespondWith(
            Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(runningPodJson)
        );

        // Act
        var handle = await _manager.StartDefaultAsync(Guid.NewGuid().ToString("N"));

        // Assert
        Assert.Equal("kubernetes", handle.Provider);
        Assert.StartsWith("sharpclaw-", handle.SandboxId);
        Assert.True(_manager.IsActive(handle.SandboxId));
    }

    [Fact]
    public async Task StopAsync_CallsKubernetesApiToDeletePod()
    {
        var pendingPodJson = @"{ ""metadata"": { ""name"": ""mock-pod-123"" }, ""status"": { ""phase"": ""Pending"" } }";
        var runningPodJson = @"{ ""metadata"": { ""name"": ""mock-pod-123"" }, ""status"": { ""phase"": ""Running"" } }";

        // Arrange
        _server.Given(
            Request.Create().WithPath("/api/v1/namespaces/default/pods").UsingPost()
        ).RespondWith(
            Response.Create().WithStatusCode(201).WithHeader("Content-Type", "application/json").WithBody(pendingPodJson)
        );

        _server.Given(
            Request.Create().WithPath(p => p != null && p.StartsWith("/api/v1/namespaces/default/pods/") && !p.EndsWith("/status")).UsingGet()
        ).RespondWith(
            Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(runningPodJson)
        );

        _server.Given(
            Request.Create().WithPath(p => p != null && p.StartsWith("/api/v1/namespaces/default/pods/")).UsingDelete()
        ).RespondWith(
            Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(runningPodJson)
        );

        var handle = await _manager.StartDefaultAsync(Guid.NewGuid().ToString("N"));

        // Act
        await _manager.StopSandboxAsync(handle.SandboxId);

        // Assert
        Assert.False(_manager.IsActive(handle.SandboxId));
        
        var logs = _server.LogEntries;
        Assert.Contains(logs, l => l.RequestMessage.Path != null && l.RequestMessage.Path.Contains("/api/v1/namespaces/default/pods/") && l.RequestMessage.Method == "DELETE");
    }

    public void Dispose()
    {
        if (File.Exists(_kubeConfigPath))
        {
            File.Delete(_kubeConfigPath);
        }

        _server.Stop();
        _server.Dispose();
    }
}
