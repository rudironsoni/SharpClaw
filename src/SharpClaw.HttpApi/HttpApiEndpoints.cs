using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SharpClaw.Abstractions.Identity;
using SharpClaw.Gateway;
using SharpClaw.OpenResponses.HttpApi;
using SharpClaw.Tenancy;
using SharpClaw.Web;

namespace SharpClaw.HttpApi;

public static class HttpApiEndpoints
{
    public static WebApplication MapSharpClawHttpApiEndpoints(this WebApplication app)
    {
        // Add tenant resolution middleware first
        app.UseMiddleware<TenantResolutionMiddleware>();
        
        app.UseWebSockets();
        app.MapGet("/", () => Results.Ok(new { service = "SharpClaw.Host", status = "ok" }));
        app.MapGet("/health", ([FromServices] GatewayHealthService healthService) => Results.Ok(healthService.Snapshot()));
        app.Map("/ws", (HttpContext context, 
            [FromServices] GatewayDispatcher dispatcher,
            [FromServices] IIdentityService identityService,
            [FromServices] ConnectionRegistry connectionRegistry,
            [FromServices] ILoggerFactory loggerFactory) => 
        {
            var logger = loggerFactory.CreateLogger(nameof(WebSocketGatewayEndpoint));
            return WebSocketGatewayEndpoint.HandleAsync(context, dispatcher, identityService, connectionRegistry, logger);
        });
        app.MapControlUiEndpoints();
        app.MapOpenResponsesEndpoints();

        return app;
    }
}
