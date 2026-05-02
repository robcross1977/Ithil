namespace Ithil.Management.Models;

/// <summary>
/// Payload for creating a new agent.
/// </summary>
public record CreateAgentRequest
{
    public required string Label { get; init; }
    public int DailyTokenBudget { get; init; }
    public List<string> AllowedTools { get; init; } = [];
    public List<string> Scopes { get; init; } = [];
}
