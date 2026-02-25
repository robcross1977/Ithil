using Ithil.Core.Models;

namespace Ithil.Gateway.Mcp.Handlers;

/// <summary>
/// Handles the MCP "resources/read" method.
/// </summary>
public class ResourcesReadHandler
{
    /// <summary>
    /// Returns a method-not-found error.
    /// </summary>
    public static JsonRpcResponse Handle(JsonRpcRequest request) =>
        JsonRpcResponse.MethodNotFound(request.Id);
}

