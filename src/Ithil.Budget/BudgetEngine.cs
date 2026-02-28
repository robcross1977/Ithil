using Ithil.Core.Interfaces;
using LanguageExt;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Ithil.Budget;

/// <summary>
/// Redis-backed implementation of IBudgetEngine.
/// Tracks per-agent daily token usage and enforces configurable limits.
/// Fails open - if Redis is unavailable, all budget checks return true. 
/// </summary>
public class BudgetEngine(
    IDatabase redis,
    BudgetEngineOptions options,
    ILogger<BudgetEngine> logger) : IBudgetEngine
{
    private readonly IDatabase _redis = redis;
    private readonly BudgetEngineOptions _options = options;
    private readonly ILogger<BudgetEngine> _logger = logger;

    /// <summary>
    /// Returns true if the agent's token usage today is below the daily limit.
    /// Returns true if Redis is unavailable (fail-open policy).
    /// </summary>
    public async Task<bool> IsWithinBudgetAsync(string agentId)
    {
        var usage = await TryGetUsageAsync(agentId);
        return usage.Match(
            Some: u => u < _options.DefaultDailyTokenLimit,
            None: () => true);
    }

    /// <summary>
    /// Increments the agent's token usage and sets a 48-hour expiry on the key
    /// </summary>
    public async Task RecordUsageAsync(string agentId, int tokens)
    {
        var key = BudgetKeyFactory.ForToday(agentId);
        await _redis.StringIncrementAsync(key, tokens);
        await _redis.KeyExpireAsync(key, TimeSpan.FromDays(2));
    }

    /// <summary>
    /// Returns the agent's token usage for today. Returns 0 if the key does not exist.
    /// </summary>
    public async Task<int> GetUsageAsync(string agentId)
    {
        var usage = await TryGetUsageAsync(agentId);
        return usage.IfNone(0);
    }

    // Reads usage from Redis and wraps it in Option<int>.
    // Returns None if the key is absent or Redis throws (fail-open).
    private async Task<Option<int>> TryGetUsageAsync(string agentId)
    {
        try
        {
            var key = BudgetKeyFactory.ForToday(agentId);
            var value = await _redis.StringGetAsync(key);
            return value.IsNull ? Option<int>.None : Option<int>.Some((int)value);
        }
        catch(RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable for agent {AgentId}; failing open", agentId);
            return Option<int>.None;
        }
    }
}
