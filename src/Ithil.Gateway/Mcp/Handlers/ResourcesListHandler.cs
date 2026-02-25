using Ithil.Core.Models;

namespace Ithil.Gateway.Mcp.Handlers;

/// <summary>
/// Handles the MCP "resource/list" method.
/// </summary>
public class ResourcesListHandler
{
    /// <summary>
    /// Returns an empty resource list. 
    /// </summary>
    public static JsonRpcResponse Handle(JsonRpcRequest request) => new()
    {
        Id = request.Id,
        Result = new { resources = Array.Empty<object>() }
    };
}

