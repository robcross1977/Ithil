using System.Text;
using System.Text.Json;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;

namespace Ithil.Gateway.Mcp.Handlers;

/// <summary>
/// Handles the MCP "tools/call" method, routing the call to the downstream service via HttpClient.
/// </summary>
public static class ToolsCallHandler
{
    /// <summary>
    /// Looks up the named tool in the registry, builds an HTTP request, and returns the downstream response
    /// wrapped as MCP content. Returns a JSON-RPC error if the tool name is unknown.
    /// </summary>
    public static async Task<JsonRpcResponse> HandleAsync(
        JsonRpcRequest request,
        IToolRegistry registry,
        IHttpClientFactory httpClientFactory,
        ToolRegistryOptions options,
        ISemanticCache semanticCache,
        ITraceNotifier traceNotifier,
        IAuditLogger auditLogger,
        IPrivacyFilter privacyFilter
    )
    {
        if (!request.Params.HasValue)
            return InvalidParams(request.Id, "Missing params");

        var p = request.Params.Value;

        if (!p.TryGetProperty("name", out var nameEl) || string.IsNullOrEmpty(nameEl.GetString()))
            return InvalidParams(request.Id, "Missing tool name");

        var toolName = nameEl.GetString()!;
        var arguments = p.TryGetProperty("arguments", out var argsEl) ? argsEl : default;

        var tools = await registry.GetToolsAsync();
        var tool = tools.FirstOrDefault(t => t.Name == toolName);

        if (tool is null)
            return InvalidParams(request.Id, $"Unknown tool: {toolName}");

        var cachedResult = await semanticCache.TryGetAsync(toolName, arguments);

        if (cachedResult.IsSome)
        {
            var cached = cachedResult.Match(r => r, () => null!);

            await traceNotifier.NotifyAsync(
                new AgentTraceEvent
                {
                    TraceId = string.Empty,
                    AgentId = string.Empty,
                    ToolName = toolName,
                    Status = "cache-hit",
                    Timestamp = DateTime.UtcNow.ToString("O"),
                }
            );

            await auditLogger.WriteAsync(new AuditRecord
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                TraceId = string.Empty,
                AgentId = string.Empty,
                ToolName = toolName,
                Outcome = "cache-hit",
                CacheHit = true,
            });

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = new
                {
                    content = new[] { new { type = "text", text = cached.SerializedResponse } },
                },
            };
        }
        var httpRequest = ToolCallRouter.BuildRequest(tool, options.DownstreamBaseUrl, arguments);
        var client = httpClientFactory.CreateClient("downstream");
        var response = await client.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        var rawArgs = arguments.ValueKind != JsonValueKind.Undefined ? arguments.ToString() : "{}";
        var scrubbedArgs = await privacyFilter.ScrubAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(rawArgs))
        );

        await auditLogger.WriteAsync(new AuditRecord
        {
            Timestamp = DateTime.UtcNow.ToString("O"),
            TraceId = string.Empty,
            AgentId = string.Empty,
            ToolName = toolName,
            Outcome = "success",
            Parameters = scrubbedArgs,
            PiiScrubbed = true,
        });

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new { content = new[] { new { type = "text", text = content } } },
        };
    }

    private static JsonRpcResponse InvalidParams(JsonElement id, string message) =>
        new()
        {
            Id = id,
            Error = new JsonRpcError { Code = -32602, Message = message },
        };
}
