using Ithil.Core.Interfaces;
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
        var agentId = context.User.FindFirst("agent_id")?.Value ?? string.Empty;
        var allowlistService = context.RequestServices.GetRequiredService<IToolAllowlistService>();
        var toolRegistry = context.RequestServices.GetRequiredService<IToolRegistry>();
        var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        var registryOptions = context.RequestServices.GetRequiredService<ToolRegistryOptions>();

        var allowlist = await allowlistService.TryGetToolAllowlistAsync(agentId);
        var allTools = await toolRegistry.GetToolsAsync(cancellationToken);

        var allowedTools = allowlist.Match(
            Some: names => allTools.Filter(t => names.Contains(t.Name)),
            None: () => allTools);

        options.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in allowedTools)
        {
            var fn = new ToolProxyAIFunction(tool, registryOptions.DownstreamBaseUrl, httpClientFactory);
            options.ToolCollection.Add(McpServerTool.Create(fn));
        }
    }
}
