namespace Ithil.Management.Models;

/// <summary>
/// Agent details returned by GET endpoints. Never includes the API key.
/// </summary>
public record AgentResponse
{
    public required string AgentId { get; init; }
    public required string Label { get; init; }
    public int DailyTokenBudget { get; init; }
    public List<string> AllowedTools { get; init; } = [];
    public List<string> Scopes { get; init; } = [];
    public bool IsActive { get; init; }
}
