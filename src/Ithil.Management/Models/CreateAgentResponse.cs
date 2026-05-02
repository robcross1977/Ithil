namespace Ithil.Management.Models;

/// <summary>
/// Returned after a successful agent creation. ApiKey is shown once and never stored.
/// </summary>
public record CreateAgentResponse
{
    public required string AgentId { get; init; }
    public required string ApiKey { get; init; }
    public required string Label { get; init; }
    public int DailyTokenBudget { get; init; }
    public List<string> AllowedTools { get; init; } = [];
    public List<string> Scopes { get; init; } = [];
    public bool IsActive { get; init; }
}
