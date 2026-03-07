using Ithil.Core.Interfaces;
using Ithil.Core.Models;

namespace Ithil.Gateway.Mcp.Handlers;

/// <summary>
/// Handles the MCP "tools/list" method, returning tools the agent is permitted to call.
/// </summary>
public static class ToolsListHandler
{
    /// <summary>
    /// Fetches the tool list from the registry and formats it as an MCP tools/list response.
    /// Routing fields (HttpMethod, RoutePattern) are stripped — the AI only sees name, description, and inputSchema.
    /// </summary>
    public static async Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, IToolRegistry registry)
    {
        var tools = await registry.GetToolsAsync();

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new
            {
                tools = tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    inputSchema = new
                    {
                        type = "object",
                        properties = t.InputSchema.Properties.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new { type = kvp.Value.Type, description = kvp.Value.Description }),
                        required = t.InputSchema.Required.ToArray()
                    }
                })
            }
        };
    }
}
