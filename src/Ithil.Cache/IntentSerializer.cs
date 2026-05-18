using System.Text.Json;

namespace Ithil.Cache;

/// <summary>
/// Produces a deterministic string representation of a tool call intent.
/// Used as input to the embedding model - two calls with the same semantic meaning.
/// must produce identical strings so their vectors are comparable. 
/// </summary>
public static class IntentSerializer
{
    // JsonSerializerOptions is expensive to construct — create once and reuse.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        // Compact output — no line breaks or indentation.
        WriteIndented = false
    };

    /// <summary>
    /// Serializes a tool call into a stable intent string of the form:
    /// "ToolName:{"paramA":"value","paramB":42}"
    /// Parameter keys are sorted alphabetically at every nesting level before
    /// serialization, so two calls with identically-valued but differently-ordered
    /// parameters produce identical strings and therefore identical embeddings.
    /// </summary>
    public static string Serialize(string toolName, object parameters)
    {
        // Serialize the parameters to a JsonDocument so we can inspect and sort the keys.
        var json = JsonSerializer.Serialize(parameters, SerializerOptions);
        var doc = JsonDocument.Parse(json);

        // If parameters is not an object (e.g. null, primitive), use it as-is.
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return $"{toolName}:{json}";

        // Recursively rebuild the parameters with all object keys sorted alphabetically.
        // This handles nested objects — e.g. {address:{zip:"EC1",city:"London"}} —
        // where a top-level-only sort would leave inner keys in insertion order.
        var sortedJson = JsonSerializer.Serialize(SortObjectKeys(doc.RootElement), SerializerOptions);
        return $"{toolName}:{sortedJson}";
    }

    // Rebuilds a JSON Object as a sorted Dictionary, recursively processing any
    // nested Object or Array values so that key ordering is stable at every level.
    private static Dictionary<string, object?> SortObjectKeys(JsonElement obj) =>
        obj.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(p => p.Name, p => SortElementValue(p.Value));

    // Returns a stable representation of any JSON value:
    // - Objects    → sorted Dictionary (recurse into nested keys)
    // - Arrays     → array with each element recursively stabilised (order preserved)
    // - Primitives → the JsonElement itself, which serializes verbatim
    private static object? SortElementValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => SortObjectKeys(element),
        JsonValueKind.Array  => element.EnumerateArray().Select(SortElementValue).ToArray(),
        _                    => (object?)element
    };
}

