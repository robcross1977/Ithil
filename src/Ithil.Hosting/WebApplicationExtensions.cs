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
        app.MapIthilSchema(tools.Select(ToResponse));

    /// <summary>
    /// Registers <c>GET /ithil/schema</c> with both discovery paths combined: the
    /// compile-time <c>[AgentTool]</c> entries you pass in, plus any minimal-API endpoints
    /// marked with <see cref="AgentToolEndpointExtensions.WithAgentTool{TBuilder}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike the other overloads this one resolves lazily, on the first request to
    /// <c>/ithil/schema</c>. It has to: <see cref="EndpointDataSource"/> is empty until the
    /// host has finished building its routing table, so a snapshot taken at registration
    /// time would always report zero minimal-API tools. The result is cached after the
    /// first request, so the routing table is walked at most once.
    /// </para>
    /// <para>
    /// When the same verb and route are found by both paths, the compile-time
    /// <c>[AgentTool]</c> entry wins. That keeps the attribute authoritative for controller
    /// actions that also happen to be reachable through a mapped route.
    /// </para>
    /// </remarks>
    /// <param name="app">The application whose routing table will be inspected.</param>
    /// <param name="generatedTools">
    /// Compile-time discovered tools, normally <c>SchemaRegistry.Tools</c>. Pass an empty
    /// sequence for an app that uses only minimal APIs.
    /// </param>
    public static IEndpointRouteBuilder MapIthilSchema(
        this WebApplication app,
        IEnumerable<ToolEntry> generatedTools,
        bool includeMappedEndpoints)
    {
        if (!includeMappedEndpoints)
            return app.MapIthilSchema(generatedTools);

        var generated = generatedTools.ToList();

        // Lazy<T> gives us both the deferred evaluation the EndpointDataSource requires and
        // thread-safe single execution, so concurrent first requests cannot race.
        var merged = new Lazy<List<ToolSchemaResponse>>(() =>
        {
            var discovered = EndpointToolDiscovery.Discover(
                app.Services.GetRequiredService<EndpointDataSource>());

            return Merge(generated, discovered).Select(ToResponse).ToList();
        });

        app.MapGet("/ithil/schema", () => Results.Ok(merged.Value));
        return app;
    }

    /// <summary>
    /// Combines compile-time and runtime tools, keyed on verb plus route pattern.
    /// Compile-time <c>[AgentTool]</c> entries take precedence over mapped endpoints.
    /// </summary>
    internal static List<ToolEntry> Merge(
        IEnumerable<ToolEntry> generated,
        IEnumerable<ToolEntry> discovered)
    {
        var merged = new List<ToolEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Generated first so the attribute wins any collision.
        foreach (var tool in generated)
        {
            if (seen.Add(RouteKey(tool)))
                merged.Add(tool);
        }

        foreach (var tool in discovered)
        {
            if (seen.Add(RouteKey(tool)))
                merged.Add(tool);
        }

        return merged;
    }

    private static string RouteKey(ToolEntry tool) =>
        $"{tool.HttpMethod}:{tool.RoutePattern.TrimStart('/')}";

    private static ToolSchemaResponse ToResponse(ToolEntry t) =>
        new(t.Name, t.Description, t.AllowWrite, t.MaxResponseTokens,
            t.Category, t.RequiredScopes, t.HttpMethod, t.RoutePattern,
            t.ParameterSources,
            ToolSchemaMapper.BuildInputSchema(t.ParameterSources, t.ParameterTypes));
}
