namespace Ithil.Dashboard.Models;

/// <summary>
/// Circuit breaker states as reported by Polly.
/// </summary>
public enum CircuitState { Closed, Open, HalfOpen }

/// <summary>
/// View model for a single monitored cluster's circuit breaker state.
/// </summary>
public record CircuitBreakerViewModel
{
    public required string AgentId { get; init; }
    public required string ToolName { get; init; }
    public required CircuitState State { get; init; }
    public required string LastChanged { get; init; }
}
