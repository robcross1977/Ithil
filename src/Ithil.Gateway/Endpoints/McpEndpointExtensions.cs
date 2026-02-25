using Ithil.Gateway.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;

namespace Ithil.Gateway.Endpoints;

/// <summary>
/// Registers all MCP protocol endpoints on the application router.
public static class McpEndpointExtensions
{
    /// <summary>
    /// Maps /.well-known/mcp, POST /mcp and GET /mcp/sse onto the route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapMcpEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/mcp", () => Results.Ok(new
        {
            tools = Array.Empty<object>(),
            resources = Array.Empty<object>()
        }));

        app.MapPost("/mcp", async context =>
        {
            var request = await JsonSerializer.DeserializeAsync<Ithil.Core.Models.JsonRpcRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if(request is null)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var dispatcher = context.RequestServices.GetRequiredService<McpDispatcher>();
            var response = await dispatcher.DispatchAsync(request);
            
            if(response is null)
            {
                context.Response.StatusCode = 204;
                return;
            }

            await context.Response.WriteAsJsonAsync(response);
        });

        app.MapGet("/mcp/sse", async context =>
        {
            var emitter = context.RequestServices.GetRequiredService<SseEmitter>();
            await emitter.StreamAsync(context);
        });

        return app;
    }
}
