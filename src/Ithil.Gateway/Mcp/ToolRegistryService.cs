using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using System.Net.Http.Json;

namespace Ithil.Gateway.Mcp;

/// <summary>
/// Fetches tool definitions from the downstream /ithil/schema endpoint and caches them in memory.
/// </summary>
public class ToolRegistryService : IToolRegistry
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ToolRegistryOptions _options;
    private readonly object _lock = new();

    // Null means not yet fetched. Non-null (even if empty) means fetch has run.
    private Seq<ToolRegistryEntry>? _cache;

    /// <summary>
    /// Initializes the registry with an HTTP client factory and configuration options.
    /// </summary>
    public ToolRegistryService(IHttpClientFactory httpClientFactory, ToolRegistryOptions options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    /// <summary>
    /// Returns all tools from the downstream schema endpoint.
    /// Result is cached in memory after the first successful fetch.
    /// Returns an empty sequence if the downstream is unreachable.
    /// </summary>
    public async Task<Seq<ToolRegistryEntry>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_cache.HasValue) return _cache.Value;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("downstream");
            var dtos = await client.GetFromJsonAsync<List<SchemaDto>>(_options.SchemaUrl, cancellationToken)
                ?? new List<SchemaDto>();

            var tools = dtos.Select(ToEntry).ToSeq();
            lock (_lock) { _cache = tools; }
            return tools;
        }
        catch (HttpRequestException)
        {
            // Downstream is unreachable — fail open with an empty registry rather than crashing.
            lock (_lock) { _cache = Seq<ToolRegistryEntry>.Empty; }
            return Seq<ToolRegistryEntry>.Empty;
        }
    }

    // Converts the JSON DTO (plain C# types) into the LanguageExt-typed ToolRegistryEntry.
    // DTOs exist because System.Text.Json cannot deserialize LanguageExt Map/Seq directly.
    private static ToolRegistryEntry ToEntry(SchemaDto dto) => new(
        dto.Name,
        dto.Description,
        dto.AllowWrite,
        dto.MaxResponseTokens,
        dto.Category,
        dto.HttpMethod,
        dto.RoutePattern,
        dto.ParameterSources ?? new(),
        new McpInputSchema
        {
            Properties = Map.createRange(
                (dto.InputSchema?.Properties ?? new())
                    .Select(kvp => (kvp.Key, new JsonSchemaProperty { Type = kvp.Value.Type, Description = kvp.Value.Description }))),
            Required = (dto.InputSchema?.Required ?? new()).ToSeq()
        });

    // Private DTOs for JSON deserialization — mirror ToolSchemaResponse using plain C# types.
    private record SchemaDto(
        string Name,
        string Description,
        bool AllowWrite,
        int MaxResponseTokens,
        string? Category,
        string HttpMethod,
        string RoutePattern,
        Dictionary<string, string>? ParameterSources,
        InputSchemaDto? InputSchema);

    private record InputSchemaDto(
        Dictionary<string, PropertyDto>? Properties,
        List<string>? Required);

    private record PropertyDto(string Type, string? Description);
}
