namespace Ithil.Core.Models;

/// <summary>
/// Describes a single property in a JSON Schema definition.
/// </summary>
public record JsonSchemaProperty
{
    /// <summary>
    /// The JSON Schema type of this property (e.g. "string", "integer", "boolean").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Human-readable description of what this property represents.
    /// </summary>
    public string? Description { get; init; }
}
