namespace Ithil.Hosting;

/// <summary>
/// Describes a single [AgentTool]-decorated endpoint as discovered at compile time
/// by the Ithil source generator. <see cref="SchemaRegistry"/> returns a list of these.
/// </summary>
public class ToolEntry
{
    /// <summary>Method name used as the tool identifier by the gateway.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable description passed to the agent.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this tool can modify data (POST/PUT/DELETE).</summary>
    public bool AllowWrite { get; set; }

    /// <summary>Maximum tokens the gateway should allocate for a response.</summary>
    public int MaxResponseTokens { get; set; }

    /// <summary>Optional grouping category shown in the MCP tool list.</summary>
    public string? Category { get; set; }

    /// <summary>JWT scopes required to call this tool.</summary>
    public string[] RequiredScopes { get; set; } = Array.Empty<string>();

    /// <summary>HTTP verb (GET, POST, etc.).</summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>ASP.NET route template, e.g. "api/orders/{id}/status".</summary>
    public string RoutePattern { get; set; } = string.Empty;

    /// <summary>Maps parameter names to their binding source (route, query, body).</summary>
    public Dictionary<string, string> ParameterSources { get; set; } = new();

    /// <summary>Maps parameter names to their JSON Schema types (string, integer, etc.).</summary>
    public Dictionary<string, string> ParameterTypes { get; set; } = new();
}
