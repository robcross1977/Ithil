namespace Ithil.Core.Models;

/// <summary>
/// Represents an outgoing JSON-RPC 2.0 response.
/// </summary>
public record JsonRpcResponse
{
    /// <summary>
    /// Always "2.0".
    /// </summary>
    public string Jsonrpc { get; init; } = "2.0";

    /// <summary>
    /// Echoes the request Id.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The result payload on success. Null if error is set.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// The error payload on failure. Null if result is set. 
    /// </summary>
    public JsonRpcError? Error { get; init; }

    /// <summary>
    /// Returns a method-not-found error response for the given request id.
    /// </summary>
    public static JsonRpcResponse MethodNotFound(string id) => new()
    {
        Id = id,
        Error = new JsonRpcError { Code = -32601, Message = "Method not found" }
    };
}
