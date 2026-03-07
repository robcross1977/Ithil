using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Hosting;

/// <summary>
/// Converts generated ToolEntry parameter data into McpInputSchema for use in ToolSchemaResponse.
/// </summary>
public static class ToolSchemaMapper
{
    /// <summary>
    /// Builds an McpInputSchema from a parameter sources dictionary.
    /// Infers "string" as the JSON type since the generator does not yet emit full type info.
    /// </summary>
    public static McpInputSchema BuildInputSchema(Dictionary<string, string> parameterSources)
    {
        var properties = parameterSources.Keys
            .ToDictionary(name => name, _ => new JsonSchemaProperty { Type = "string" });

        return new McpInputSchema
        {
            Properties = Map.createRange(properties),
            Required = Seq.createRange(parameterSources.Keys.ToList())
        };
    }
}
