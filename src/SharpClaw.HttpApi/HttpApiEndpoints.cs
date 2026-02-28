using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SharpClaw.Gateway;
using SharpClaw.OpenResponses.HttpApi;
using SharpClaw.Web;

namespace SharpClaw.HttpApi;

public static class HttpApiEndpoints
{
    public static WebApplication MapSharpClawHttpApiEndpoints(this WebApplication app)
    {
        app.UseWebSockets();
        app.MapGet("/", () => Results.Ok(new { service = "SharpClaw.Host", status = "ok" }));
        app.MapGet("/health", ([FromServices] GatewayHealthService healthService) => Results.Ok(healthService.Snapshot()));
        app.Map("/ws", (HttpContext context, [FromServices] GatewayDispatcher dispatcher) => WebSocketGatewayEndpoint.HandleAsync(context, dispatcher));
        app.MapControlUiEndpoints();
        app.MapOpenResponsesEndpoints();

        return app;
    }
}
