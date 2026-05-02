namespace Ithil.Budget;

/// <summary>
/// Produces Redis key strings for per-agent daily token budgets.
/// </summary>
public static class BudgetKeyFactory
{
    /// <summary>
    /// Returns the Redis key for the given agent's usage on today's UTC date.
    /// </summary>
    public static string ForToday(string agentId) =>
        ForDate(agentId, DateTime.UtcNow);

    /// <summary>
    /// Returns the Redis key for the given agent's usage on a specific UTC date.
    /// </summary>
    public static string ForDate(string agentId, DateTime utcDate) =>
        $"budget:{agentId}:{utcDate:yyyyMMdd}";
}
