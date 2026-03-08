using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SharpClaw.Persistence.Core;

namespace SharpClaw.Web.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration testing
/// </summary>
public class SharpClawWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override JWT settings for testing with a valid 32+ character secret
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "this-is-a-test-secret-key-that-is-32-chars-long-for-jwt-signing!",
                ["Jwt:Issuer"] = "SharpClawTest",
                ["Jwt:Audience"] = "SharpClawTestClients"
            });
        });
    }
}
