using System.Text.Json;
using System.Text.Json.Nodes;
using Ithil.Core.Models;
using Ithil.Gateway.Transforms;
using Microsoft.Extensions.AI;

namespace Ithil.Gateway.Mcp;

/// <summary>
/// Forwards MCP tool calls to the downstream HTTP API through the governance pipeline.
/// </summary>
internal sealed class ToolProxyAIFunction(
    ToolRegistryEntry tool,
    string downstreamBaseUrl,
    IHttpClientFactory httpClientFactory,
    string agentId,
    ToolCallGovernancePipeline governance) : AIFunction
{
    public override string Name => tool.Name;
    public override string Description => tool.Description;
    public override JsonElement JsonSchema => BuildSchema(tool.InputSchema);

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var argsJson = JsonSerializer.SerializeToElement(
            arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        var request = ToolCallRouter.BuildRequest(tool, downstreamBaseUrl, argsJson);
        var client = httpClientFactory.CreateClient("downstream");

        return await governance.ExecuteAsync(
            agentId,
            tool.Name,
            async () =>
            {
                var response = await client.SendAsync(request, cancellationToken);
                return await response.Content.ReadAsStringAsync(cancellationToken);
            },
            cancellationToken);
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
