using LanguageExt;

namespace Ithil.Core.Models;

/// <summary>
/// Describes the input parameters for an MCP tool, following JSON Schema conventioons.
/// </summary>
public record McpInputSchema
{
    /// <summary>
    /// The named input properties the tool accepts, each with its schema definition.
    /// </summary>
    public Map<string, JsonSchemaProperty> Properties { get; init; } = Map<String, JsonSchemaProperty>.Empty;

    /// <summary>
    /// Names of properties that must be provided when calling this tool.
    /// </summary>
    public Seq<string> Required { get; init; } = Seq<string>.Empty;
}
