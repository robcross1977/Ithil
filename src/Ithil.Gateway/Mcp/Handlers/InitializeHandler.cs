using Ithil.Core.Models;

namespace Ithil.Gateway.Mcp.Handlers;

/// <summary>
/// Handles the MCP "initialize" method, returning the protocol version and server capabilities. 
/// </summary>
public class InitializeHandler
{
    /// <summary>
    /// Returns the protocol version and server info required to complete MCP handshake.
    /// </summary>
    public static JsonRpcResponse Handle(JsonRpcRequest request) => new()
    {
        Id = request.Id,
        Result = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { tools = new {}, resources = new {} },
            serverInfo = new { name = "Ithil", version = "1.0.0" }
        }
    };
}

