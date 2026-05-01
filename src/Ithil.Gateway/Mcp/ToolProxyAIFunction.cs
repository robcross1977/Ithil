using System.Text.Json;
using System.Text.Json.Nodes;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Transforms;
using Microsoft.Extensions.AI;

namespace Ithil.Gateway.Mcp;

/// <summary>
/// Forwards MCP tool calls to the downstream HTTP API through the governance pipeline.
/// Checks the semantic cache before calling downstream and writes back on success.
/// </summary>
internal sealed class ToolProxyAIFunction(
    ToolRegistryEntry tool,
    string downstreamBaseUrl,
    IHttpClientFactory httpClientFactory,
    string agentId,
    ToolCallGovernancePipeline governance,
    ISemanticCache semanticCache) : AIFunction
{
    // Default TTL for cached tool responses. Long enough to be useful; short enough to
    // avoid serving stale inventory/order data for more than a working day.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(8);

    public override string Name => tool.Name;
    public override string Description => tool.Description;
    public override JsonElement JsonSchema => BuildSchema(tool.InputSchema);

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var argsDict = arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var argsJson = JsonSerializer.SerializeToElement(argsDict);

        // Check semantic cache first — returns a hit if a sufficiently similar call was
        // made recently, avoiding a round-trip to the downstream API.
        var cached = await semanticCache.TryGetAsync(tool.Name, argsDict);
        if (cached.IsSome)
        {
            await governance.RecordCacheHitAsync(agentId, tool.Name, cancellationToken);
            return cached.Case is CacheResult hit ? hit.SerializedResponse : null;
        }

        var request = ToolCallRouter.BuildRequest(tool, downstreamBaseUrl, argsJson);
        var client = httpClientFactory.CreateClient("downstream");

        var result = await governance.ExecuteAsync(
            agentId,
            tool.Name,
            async () =>
            {
                using var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            },
            cancellationToken,
            tool.MaxResponseTokens);

        // Write back to cache so future similar calls can skip the downstream hop.
        await semanticCache.SetAsync(tool.Name, argsDict, result, CacheTtl);

        return result;
    }

    private static JsonElement BuildSchema(McpInputSchema schema)
    {
        var props = new JsonObject();
        foreach (var (name, prop) in schema.Properties)
        {
            var p = new JsonObject { ["type"] = prop.Type };
            if (prop.Description is not null)
                p["description"] = prop.Description;
            props[name] = p;
        }
        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = new JsonArray(
                schema.Required.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray())
        });
    }
}
