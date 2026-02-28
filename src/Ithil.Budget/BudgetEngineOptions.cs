namespace Ithil.Budget;

/// <summary>
/// Configuration options for the Budget Engine.
/// </summary>
public class BudgetEngineOptions
{
    /// <summary>
    /// Maximum tokens an agent may consume per day. Defaults to 100,000.
    /// </summary>
    public int DefaultDailyTokenLimit { get; set; } = 100_000;
}
