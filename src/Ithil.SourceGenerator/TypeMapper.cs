using Microsoft.CodeAnalysis;

namespace Ithil.SourceGenerator;

/// <summary>
/// Maps C# type symbols to JSON Schema type strings.
/// </summary>
public class TypeMapper
{
    /// <summary>
    /// Returns the JSON Schema type and optional format for the given C# type symbol.
    /// </summary>
    public static (string JsonType, string? Format) ToJsonType(ITypeSymbol typeSymbol)
    {
        var typeName = typeSymbol.OriginalDefinition.ToDisplayString();

        return typeName switch
        {
            "int" or "long" or "short" or "byte"    => ("integer", null),
            "float" or "double" or "decimal"        => ("number", null),
            "bool"                                  => ("boolean", null),
            "System.DateOnly" or "System.DateTime"  => ("string", "date"),
            _                                       => ("string", null)
        };
    }
}
