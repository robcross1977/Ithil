namespace Ithil.Dashboard.Models;

/// <summary>
/// View model for a single agent's token budget consumption.
/// </summary>
public record BudgetViewModel
{
    public required string AgentId { get; init; }
    public required int DailyBudget { get; init; }
    public required int TokensUsedToday { get; init; }
    public required double PercentageUsed { get; init; }
    public required bool IsWarning { get; init; }
}
