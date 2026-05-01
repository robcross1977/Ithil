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
    /// Registers GET /ithil/schema, returning all AgentTool-decorated methods
    /// as JSON the Ithil gateway can fetch and cache.
    /// The caller is responsible for converting SchemaRegistry.Tools to ToolSchemaResponse
    /// since SchemaRegistry is generated into the consuming project, not Ithil.Hosting.
    /// </summary>
    public static IEndpointRouteBuilder MapIthilSchema(
        this WebApplication app,
        IEnumerable<ToolSchemaResponse> tools)
    {
        var snapshot = tools.ToList();

        app.MapGet("/ithil/schema", () => Results.Ok(snapshot));

        return app;
    }
}
