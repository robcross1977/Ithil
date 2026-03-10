namespace Ithil.Hosting;

/// <summary>
/// Converts generated ToolEntry parameter data into a serializable ToolInputSchema.
/// </summary>
public static class ToolSchemaMapper
{
    /// <summary>
    /// Builds a ToolInputSchema from parameter sources.
    /// Infers "string" as the JSON type since the generator does not emit full type info.
    /// </summary>
    public static ToolInputSchema BuildInputSchema(Dictionary<string, string> parameterSources) =>
        new(
            parameterSources.Keys.ToDictionary(name => name, _ => new ToolSchemaProperty("string", null)),
            parameterSources.Keys.ToList());
}
