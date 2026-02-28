namespace Ithil.Core.Interfaces;

/// <summary>
/// Manages per-agent daily token budgets.
/// </summary>
public interface IBudgetEngine
{
    /// <summary>
    /// Returns true if the agent has remaining budget for today.
    /// </summary>
    Task<bool> IsWithinBudgetAsync(string agentId);

    /// <summary>
    /// Increments the agent's token usage by the given amount. 
    /// </summary>
    Task RecordUsageAsync(string agentId, int tokens);

    /// <summary>
    /// Manages per-agent daily token budgets.
    /// </summary>
    Task<int> GetUsageAsync(string agentId);
}
