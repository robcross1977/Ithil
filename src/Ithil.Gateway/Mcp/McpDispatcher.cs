using Ithil.Core.Models;
using Ithil.Gateway.Mcp.Handlers;

namespace Ithil.Gateway.Mcp;

/// <summary>
/// Routes incoming JSON-RPC 2.0 requests to the appropriate MCP method handler.
/// </summary>
public class McpDispatcher
{
    /// <summary>
    /// Dispatches the request to the correct handler based on the method field.
    /// Returns null for one-way notifications that require no response.
    /// </summary>
    public Task<JsonRpcResponse?> DispatchAsync(JsonRpcRequest request) =>
        Task.FromResult<JsonRpcResponse?>(request.Method switch
        {
            "initialize"                => InitializeHandler.Handle(request),
            "notifications/initialized" => null,
            "tools/list"                => ToolsListHandler.Handle(request),
            "tools/call"                => ToolsCallHandler.Handle(request),
            "resources/list"            => ResourcesReadHandler.Handle(request),
            "resources/read"            => ResourcesReadHandler.Handle(request),
            _                           => JsonRpcResponse.MethodNotFound(request.Id)
        });
}
