using Ithil.Core.Models;

namespace Ithil.Gateway.Mcp.Handlers;

/// <summary>
/// Handles the MCP "tools/list" methood, returning tools the agent is permitted to call. 
/// </summary>
public class ToolsListHandler
{
    /// <summary>
    /// Returns an empty tool list until the Source Generator populates the schema registry.
    /// </summary>
    public static JsonRpcResponse Handle(JsonRpcRequest request) => new()
    {
        Id = request.Id,
        Result = new { tools = Array.Empty<object>() }
    };
}
