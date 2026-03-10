using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Mcp.Handlers;

namespace Ithil.Gateway.Mcp;

/// <summary>
/// Routes incoming JSON-RPC 2.0 requests to the appropriate MCP method handler.
/// </summary>
public class McpDispatcher
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ToolRegistryOptions _options;

    /// <summary>
    /// Initializes the dispatcher with the services handlers need to process tool requests.
    /// </summary>
    public McpDispatcher(
        IToolRegistry toolRegistry,
        IHttpClientFactory httpClientFactory,
        ToolRegistryOptions options)
    {
        _toolRegistry = toolRegistry;
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    /// <summary>
    /// Dispatches the request to the correct handler based on the method field.
    /// Returns null for one-way notifications that require no response.
    /// </summary>
    public Task<JsonRpcResponse?> DispatchAsync(JsonRpcRequest request)
    {
        // JSON-RPC notifications have no "id". Never send a response to a notification.
        if (request.Id.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            return Task.FromResult<JsonRpcResponse?>(null);

        return request.Method switch
        {
            "initialize"                => Task.FromResult<JsonRpcResponse?>(InitializeHandler.Handle(request)),
            "notifications/initialized" => Task.FromResult<JsonRpcResponse?>(null),
            "tools/list"                => DispatchToolsList(request),
            "tools/call"                => DispatchToolsCall(request),
            "resources/list"            => Task.FromResult<JsonRpcResponse?>(ResourcesReadHandler.Handle(request)),
            "resources/read"            => Task.FromResult<JsonRpcResponse?>(ResourcesReadHandler.Handle(request)),
            _                           => Task.FromResult<JsonRpcResponse?>(JsonRpcResponse.MethodNotFound(request.Id))
        };
    }

    private async Task<JsonRpcResponse?> DispatchToolsList(JsonRpcRequest request) =>
        await ToolsListHandler.HandleAsync(request, _toolRegistry);

    private async Task<JsonRpcResponse?> DispatchToolsCall(JsonRpcRequest request) =>
        await ToolsCallHandler.HandleAsync(request, _toolRegistry, _httpClientFactory, _options);
}
