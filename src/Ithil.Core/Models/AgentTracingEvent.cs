namespace Ithil.Core.Models;

/// <summary>
/// Represents a single trace event fired after a request completes, broadcast to the dashboard via SignalR.
/// </summary>
public record AgentTraceEvent
{
    /// <summary>
    /// The trace ID stamped on the originating request.
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// The ID of the agent that made the request. 
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// The name of the tool that was called.
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// The outcome of the request (e.g. "success", "error", "blocked").
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// How long the request took in milliseconods.
    /// </summary>
    public int LatencyMs { get; init; }

    /// <summary>
    /// Number of tokens consumed by this request.
    /// </summary>
    public int TokensUsed { get; init ; }

    /// <summary>
    /// Circuit breaker state change, if this event was triggered by a circuit transititon.
    /// "open" when the circuit just opened; "closed" when it just closed. Null otherwise.
    /// </summary>
    public string? CircuitState { get; init; }
}
