namespace Ithil.Management.Models;

/// <summary>
/// Current token budget usage for an agent.
/// </summary>
public record BudgetStatusResponse
{
    public required string AgentId { get; init; }
    public int TokensUsedToday { get; init; }
    public int DailyBudget { get; init; }
    public double PercentageUsed { get; init; }
    public DateTime ResetsAt { get; init; }
}
