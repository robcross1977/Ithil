using Microsoft.AspNetCore.Builder;

namespace Ithil.Hosting;

/// <summary>
/// Fluent registration of minimal-API endpoints as MCP-callable tools.
/// </summary>
public static class AgentToolEndpointExtensions
{
    /// <summary>
    /// Marks a minimal-API endpoint as an MCP-callable tool, the runtime equivalent of
    /// putting <c>[AgentTool]</c> on a controller action.
    /// </summary>
    /// <remarks>
    /// Marking is explicit and opt-in: an endpoint without this call is never exposed to an
    /// agent. Adding a route can therefore never silently widen what agents can reach.
    ///
    /// <para>
    /// The tool <paramref name="name"/> is required rather than inferred. Minimal-API handlers
    /// are usually lambdas, whose compiler-generated method names (<c>&lt;&lt;Main&gt;$&gt;b__0_1</c>)
    /// are neither stable nor meaningful to an agent. An explicit name is also refactor-safe:
    /// renaming the handler cannot silently rename a tool that agents call.
    /// </para>
    ///
    /// <example>
    /// <code>
    /// app.MapGet("/api/orders/{id}", (int id) => ...)
    ///    .WithAgentTool("GetOrder", "Retrieves a single order by id");
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="builder">The endpoint being registered.</param>
    /// <param name="name">Tool identifier the agent calls. Must be non-empty.</param>
    /// <param name="description">What the tool does. Must be non-empty.</param>
    /// <param name="allowWrite">Whether the tool may modify data. Defaults to false.</param>
    /// <param name="maxResponseTokens">Token ceiling for responses. Defaults to 2000.</param>
    /// <param name="category">Optional grouping category for the dashboard tool library.</param>
    /// <param name="requiredScopes">Optional OAuth scopes the calling agent must hold.</param>
    /// <exception cref="ArgumentException">
    /// Thrown at registration when <paramref name="name"/> or <paramref name="description"/> is empty.
    /// </exception>
    public static TBuilder WithAgentTool<TBuilder>(
        this TBuilder builder,
        string name,
        string description,
        bool allowWrite = false,
        int maxResponseTokens = 2000,
        string? category = null,
        string[]? requiredScopes = null)
        where TBuilder : IEndpointConventionBuilder
    {
        var metadata = new AgentToolMetadata(
            name, description, allowWrite, maxResponseTokens, category, requiredScopes);

        builder.Add(endpoint => endpoint.Metadata.Add(metadata));
        return builder;
    }
}
