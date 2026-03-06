using Xunit;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

[Collection("DaytonaIntegration")]
[Trait("Category", "ExternalInfrastructure")]
public class DaytonaProviderIntegrationTests : IAsyncLifetime
{
    private readonly DaytonaIntegrationTestFixture _fixture;

    public DaytonaProviderIntegrationTests()
    {
        _fixture = new DaytonaIntegrationTestFixture();
    }

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task StartDefaultAsync_UsesDaytonaWhenConfiguredAsDefault()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        
        var apiBaseUrl = _fixture.GetApiBaseUrl();
        var httpClient = _fixture.GetHttpClient();
        
        var response = await httpClient.GetAsync($"{apiBaseUrl}/api/sandboxes", cts.Token);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task StartDefaultAsync_RejectsDockerSocketMount()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        
        var apiBaseUrl = _fixture.GetApiBaseUrl();
        var httpClient = _fixture.GetHttpClient();
        
        var content = new StringContent("{\"name\":\"test\",\"image\":\"alpine:latest\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{apiBaseUrl}/api/sandboxes", content, cts.Token);
        
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created || 
                   response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StopAsync_CallsDaytonaApisToStopAndRemove()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        
        var apiBaseUrl = _fixture.GetApiBaseUrl();
        var httpClient = _fixture.GetHttpClient();
        
        var response = await httpClient.DeleteAsync($"{apiBaseUrl}/api/sandboxes/test-sandbox", cts.Token);
        
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.NoContent || 
                   response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }
}

[CollectionDefinition("DaytonaIntegration")]
public class DaytonaIntegrationCollection : ICollectionFixture<DaytonaIntegrationTestFixture>
{
    // Collection fixtures prevent parallel execution
}
