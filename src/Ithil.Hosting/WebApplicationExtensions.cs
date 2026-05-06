using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ithil.Hosting;

/// <summary>
/// Extension methods for registering Ithil services and endpoints on the downstream service.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Registers Ithil downstream services with the dependency injection container.
    /// Call this in Program.cs before building the app.
    /// </summary>
    public static IServiceCollection AddIthilHosting(this IServiceCollection services)
    {
        // Hook point for future Ithil downstream service registrations.
        return services;
    }

    /// <summary>
    /// Registers <c>GET /ithil/schema</c>, returning all AgentTool-decorated methods
    /// as JSON the Ithil gateway can fetch and cache.
    /// </summary>
    public static IEndpointRouteBuilder MapIthilSchema(
        this WebApplication app,
        IEnumerable<ToolSchemaResponse> tools)
    {
        var snapshot = tools.ToList();
        app.MapGet("/ithil/schema", () => Results.Ok(snapshot));
        return app;
    }

    /// <summary>
    /// Convenience overload — pass <c>SchemaRegistry.Tools</c> directly.
    /// Converts <see cref="ToolEntry"/> to <see cref="ToolSchemaResponse"/> internally.
    /// </summary>
    public static IEndpointRouteBuilder MapIthilSchema(
        this WebApplication app,
        IEnumerable<ToolEntry> tools) =>
        app.MapIthilSchema(tools.Select(t => new ToolSchemaResponse(
            t.Name, t.Description, t.AllowWrite, t.MaxResponseTokens,
            t.Category, t.RequiredScopes, t.HttpMethod, t.RoutePattern,
            t.ParameterSources,
            ToolSchemaMapper.BuildInputSchema(t.ParameterSources, t.ParameterTypes))));
}
