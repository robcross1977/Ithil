namespace Ithil.Hosting;

/// <summary>
/// Converts generated ToolEntry parameter data into a serializable ToolInputSchema.
/// </summary>
public static class ToolSchemaMapper
{
    /// <summary>
    /// Builds a ToolInputSchema from parameter sources and their JSON Schema types.
    /// </summary>
    public static ToolInputSchema BuildInputSchema(
        Dictionary<string, string> parameterSources,
        Dictionary<string, string>? parameterTypes = null) =>
        new(
            parameterSources.Keys.ToDictionary(
                name => name,
                name => new ToolSchemaProperty(
                    parameterTypes != null && parameterTypes.TryGetValue(name, out var t) ? t : "string",
                    null)),
            [.. parameterSources.Keys]);
}
