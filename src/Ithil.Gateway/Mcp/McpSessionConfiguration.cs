using Ithil.Core.Interfaces;
using Ithil.Gateway.Transforms;
using ModelContextProtocol.Server;

namespace Ithil.Gateway.Mcp;

/// <summary>
/// Configures per-session MCP tool visibility based on the authenticated agent's allowlist.
/// </summary>
public static class McpSessionConfiguration
{
    /// <summary>
    /// Callback for <c>WithHttpTransport</c>'s <c>ConfigureSessionOptions</c>.
    /// Filters the session's tool collection to only tools the agent is permitted to call.
    /// </summary>
    public static async Task ConfigureSessionAsync(
        HttpContext context,
        McpServerOptions options,
        CancellationToken cancellationToken)
    {
        var agentId = context.User.FindFirst("agent_id")?.Value;
        if (string.IsNullOrEmpty(agentId))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("agent_id claim is required.");
            return;
        }

        var allowlistService = context.RequestServices.GetRequiredService<IToolAllowlistService>();
        var toolRegistry = context.RequestServices.GetRequiredService<IToolRegistry>();
        var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        var registryOptions = context.RequestServices.GetRequiredService<ToolRegistryOptions>();
        var governance = context.RequestServices.GetRequiredService<ToolCallGovernancePipeline>();
        var semanticCache = context.RequestServices.GetRequiredService<ISemanticCache>();

        var allowlist = await allowlistService.TryGetToolAllowlistAsync(agentId);
        var allTools = await toolRegistry.GetToolsAsync(cancellationToken);

        var allowedTools = allowlist.Match(
            Some: names =>
            {
                // HashSet for O(1) per-tool lookup instead of O(|allowlist|) per tool.
                var nameSet = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
                return allTools.Filter(t => nameSet.Contains(t.Name));
            },
            None: () => allTools);

        // Exclude tools with no HTTP method — they were registered without an [Http*] attribute
        // and would always throw at invocation. The source generator emits ITHIL001 for these.
        var invocableTools = allowedTools.Filter(t => !string.IsNullOrEmpty(t.HttpMethod));

        options.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in invocableTools)
        {
            var fn = new ToolProxyAIFunction(tool, registryOptions.DownstreamBaseUrl, httpClientFactory, agentId, governance, semanticCache);
            options.ToolCollection.Add(McpServerTool.Create(fn));
        }
    }
}
