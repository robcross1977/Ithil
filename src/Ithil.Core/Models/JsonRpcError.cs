namespace Ithil.Core.Models;

/// <summary>
/// Represents a JSON-RPC 2.0 error object returned when a request fails.
/// </summary>
public record JsonRpcError
{
    /// <summary>
    /// The error code. (ex. -32601 indicates method not found.)
    /// </summary>
    public required int Code { get; init; }

    /// <summary>
    /// Human-readable description of the error.
    /// </summary>
    public required string Message { get; init; }
}
