namespace Ithil.Gateway.Mcp;

/// <summary>
/// Configuration for how the gateway discovers tools from the downstream service.
/// </summary>
public class ToolRegistryOptions
{
    /// <summary>
    /// The base URL of the downstream service (e.g. "http://localhost:5200").
    /// Used both to fetch the schema and to route tool calls.
    /// </summary>
    public string DownstreamBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The path of the schema endpoint on the downstream service.
    /// </summary>
    public string SchemaPath { get; set; } = "/ithil/schema";

    /// <summary>
    /// The full URL of the downstream schema endpoint, derived from base URL and path.
    /// </summary>
    public string SchemaUrl => $"{DownstreamBaseUrl.TrimEnd('/')}{SchemaPath}";
}
