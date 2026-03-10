using System.Text.Json;
using System.Text.Json.Serialization;

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
    /// Echoes the request Id. Per JSON-RPC 2.0 spec, may be a string, number, or null.
    /// </summary>
    public JsonElement Id { get; init; }

    /// <summary>
    /// The result payload on success. Omitted from JSON when null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    /// <summary>
    /// The error payload on failure. Omitted from JSON when null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; init; }

    /// <summary>
    /// Returns a method-not-found error response for the given request id.
    /// </summary>
    public static JsonRpcResponse MethodNotFound(JsonElement id) => new()
    {
        Id = id,
        Error = new JsonRpcError { Code = -32601, Message = "Method not found" }
    };
}
