namespace Ithil.Hosting;

/// <summary>
/// Endpoint metadata marking a minimal-API endpoint as an MCP-callable tool.
/// </summary>
/// <remarks>
/// This is the runtime counterpart to <c>[AgentTool]</c>. The attribute is read at compile
/// time by the source generator; this marker is attached to the endpoint by
/// <see cref="AgentToolEndpointExtensions.WithAgentTool{TBuilder}"/> and read back after
/// startup by <see cref="EndpointToolDiscovery"/>.
///
/// Both paths converge on <see cref="ToolEntry"/>, so nothing downstream — the gateway,
/// the MCP session, the dashboard — needs to know which route a tool came from.
/// </remarks>
public sealed class AgentToolMetadata
{
    /// <summary>Creates metadata describing a minimal-API endpoint as an agent tool.</summary>
    /// <param name="name">Tool identifier the agent calls. Must be non-empty.</param>
    /// <param name="description">Human-readable description of what this tool does. Must be non-empty.</param>
    /// <param name="allowWrite">Whether the tool may modify data. Defaults to false.</param>
    /// <param name="maxResponseTokens">Token ceiling for responses. Defaults to 2000.</param>
    /// <param name="category">Optional grouping category for the dashboard tool library.</param>
    /// <param name="requiredScopes">Optional OAuth scopes the calling agent must hold.</param>
    /// <param name="httpMethod">
    /// Verb the agent should call. Required only when the endpoint does not resolve to exactly
    /// one verb — a verbless <c>Map</c>, or a multi-verb <c>MapMethods</c>.
    /// </param>
    public AgentToolMetadata(
        string name,
        string description,
        bool allowWrite = false,
        int maxResponseTokens = 2000,
        string? category = null,
        string[]? requiredScopes = null,
        string? httpMethod = null)
    {
        // Fail at startup rather than serving a nameless or undescribed tool. The generator
        // catches these at compile time (ITHIL002) for attributed methods; minimal-API
        // endpoints are only knowable at runtime, so this is the earliest possible check.
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An agent tool requires a non-empty name.", nameof(name));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(
                $"Agent tool '{name}' requires a non-empty description — an agent cannot use a tool it cannot understand.",
                nameof(description));

        Name = name;
        Description = description;
        AllowWrite = allowWrite;
        MaxResponseTokens = maxResponseTokens;
        Category = category;
        RequiredScopes = requiredScopes ?? Array.Empty<string>();
        HttpMethod = string.IsNullOrWhiteSpace(httpMethod) ? null : httpMethod!.ToUpperInvariant();
    }

    /// <summary>Tool identifier the agent calls.</summary>
    public string Name { get; }

    /// <summary>Human-readable description passed to the agent.</summary>
    public string Description { get; }

    /// <summary>Whether this tool can modify data.</summary>
    public bool AllowWrite { get; }

    /// <summary>Maximum tokens the gateway should allocate for a response.</summary>
    public int MaxResponseTokens { get; }

    /// <summary>Optional grouping category shown in the MCP tool list.</summary>
    public string? Category { get; }

    /// <summary>JWT scopes required to call this tool.</summary>
    public string[] RequiredScopes { get; }

    /// <summary>
    /// Explicitly chosen verb, upper-cased, or <c>null</c> to infer it from the endpoint.
    /// </summary>
    public string? HttpMethod { get; }
}
