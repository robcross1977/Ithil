using Ithil.Core.Enums;

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

    /// <summary>
    /// How the budget engine behaves when Redis is unavailable.
    /// FailOpen passes requests through; FailClosed propagates the Redis failure,
    /// which the gateway pipeline surfaces as 503 Service Unavailable.
    /// Defaults to FailOpen to preserve existing behaviour.
    /// </summary>
    public RedisFailurePolicy FailurePolicy { get; set; } = RedisFailurePolicy.FailOpen;
}
