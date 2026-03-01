using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpClaw.Persistence.Core;

namespace SharpClaw.Web.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for integration testing
/// </summary>
public class SharpClawWebApplicationFactory : WebApplicationFactory<Program>
{
    // The Program.cs already configures InMemory database, so we don't need to override
    // Just use this factory to ensure tests get a fresh WebApplication instance
}
