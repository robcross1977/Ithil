namespace Ithil.Hosting;

/// <summary>
/// The JSON shape returned from GET /ithil/schema.
/// Uses plain C# types so System.Text.Json serializes it correctly.
/// </summary>
public record ToolSchemaResponse(
    string Name,
    string Description,
    bool AllowWrite,
    int MaxResponseTokens,
    string? Category,
    string HttpMethod,
    string RoutePattern,
    Dictionary<string, string> ParameterSources,
    ToolInputSchema InputSchema);

/// <summary>
/// Serializable input schema — plain dictionary and list so JSON output is an object, not an array.
/// </summary>
public record ToolInputSchema(
    Dictionary<string, ToolSchemaProperty> Properties,
    List<string> Required);

/// <summary>
/// A single property descriptor in the input schema.
/// </summary>
public record ToolSchemaProperty(string Type, string? Description);
