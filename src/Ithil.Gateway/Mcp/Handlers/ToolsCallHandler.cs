using Ithil.Core.Models;

namespace Ithil.Gateway.Mcp.Handlers;

/// <summary>
/// Handles the MCP "tools/call" method, routing the call to the downstream service via YARP.
/// </summary>
public class ToolsCallHandler
{
    /// <summary>
    /// Forwards the tool call to the downstream service.
    /// Full routing implemented when YARP transform pipeline is complete.
    /// </summary>
    public static JsonRpcResponse Handle(JsonRpcRequest request) => new()
    {
        Id = request.Id,
        Result = new { content = Array.Empty<object>() }
    };
}

