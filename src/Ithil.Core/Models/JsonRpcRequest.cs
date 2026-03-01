using System.Text.Json;

namespace Ithil.Core.Models;

/// <summary>
/// Represents an incoming JSON-RPC 2.0 request.
/// </summary>
public record JsonRpcRequest
{
    /// <summary>
    /// Must be "2.0".
    /// </summary>
    public required string Jsonrpc { get; init; }

    /// <summary>
    /// Client-supplied identifier echoed back in the response.
    /// Per JSON-RPC 2.0 spec, may be a string, number, or null.
    /// </summary>
    public JsonElement Id { get; init; }

    /// <summary>
    /// The method to invoke (e.g. "tools/list", "initialize").
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Optional method parameters.
    /// </summary>
    public JsonElement? Params { get; init; }
}
