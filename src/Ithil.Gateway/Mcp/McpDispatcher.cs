using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Mcp.Handlers;

namespace Ithil.Gateway.Mcp;

/// <summary>
/// Routes incoming JSON-RPC 2.0 requests to the appropriate MCP method handler.
/// </summary>
/// <remarks>
/// Initializes the dispatcher with the services handlers need to process tool requests.
/// </remarks>
public class McpDispatcher(
    IToolRegistry toolRegistry,
    IHttpClientFactory httpClientFactory,
    ToolRegistryOptions options,
    ISemanticCache semanticCache,
    ITraceNotifier traceNotifier,
    IAuditLogger auditLogger,
    IPrivacyFilter privacyFilter)
{
    private readonly IToolRegistry _toolRegistry = toolRegistry;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ToolRegistryOptions _options = options;
    private readonly ISemanticCache _semanticCache = semanticCache;
    private readonly ITraceNotifier _traceNotifier = traceNotifier;
    private readonly IAuditLogger _auditLogger = auditLogger;
    private readonly IPrivacyFilter _privacyFilter = privacyFilter;

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
        await ToolsCallHandler.HandleAsync(request, _toolRegistry, _httpClientFactory, _options, _semanticCache, _traceNotifier, _auditLogger, _privacyFilter);
}
