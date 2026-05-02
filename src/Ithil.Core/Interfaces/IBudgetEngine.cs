namespace Ithil.Core.Interfaces;

/// <summary>
/// Manages per-agent daily token budgets.
/// </summary>
public interface IBudgetEngine
{
    /// <summary>
    /// Returns true if the agent has remaining budget for today.
    /// </summary>
    Task<bool> IsWithinBudgetAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the agent's token usage by the given amount.
    /// </summary>
    Task RecordUsageAsync(string agentId, int tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the agent's token usage for today.
    /// </summary>
    Task<int> GetUsageAsync(string agentId);

    /// <summary>
    /// Resets the agent's token usage to zero for today.
    /// </summary>
    Task ResetUsageAsync(string agentId, CancellationToken cancellationToken = default);
}
