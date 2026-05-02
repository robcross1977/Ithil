using System.Text.Json;

namespace Ithil.Cache;

/// <summary>
/// Produces a deterministic string representation of a tool call intent.
/// Used as input to the embedding model - two calls with the same semantic meaning.
/// must produce identical strings so their vectors are comparable. 
/// </summary>
public static class IntentSerializer
{
    // JsonSerializerOptions is expensive to contruct - create once and reuse.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        // Sort dictionary keys alphabetically so parameter order doesn't matter.
        // Without this, {"a":1,"b":2} and {"b":2,"a":1} would produce different
        // strings and therefore different embeddings, causing a cache miss for
        // what is semantically the same call. 
        WriteIndented = false
    };

    /// <summary>
    /// Serializes a tool call into a stable intent string of the form:
    /// "ToolName:{"paramA":"value","paramB":42}"
    /// Parameter keys are sorted alphabetically before serialization.
    /// </summary>
    public static string Serialize(string toolName, object parameters)
    {
        // Serialize the parameters to a JsonDocument so we can inspect and sort the keys.
        var json = JsonSerializer.Serialize(parameters, SerializerOptions);
        var doc = JsonDocument.Parse(json);

        // If parameters is not an object (e.g. null, primitive), use it as-is.
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return $"{toolName}:{json}";

        // Rebuild the parameters dictionary with keys sorted alphabetically.
        // This ensures {"warehouseId":"UK-01","productId":42} produces the same
        // intent string as {"productId":42,"warehouseId":"UK-01"}.
        var sorted = doc.RootElement.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(p => p.Name, p => p.Value);

        var sortedJson = JsonSerializer.Serialize(sorted, SerializerOptions);
        return $"{toolName}:{sortedJson}";
    }
}

