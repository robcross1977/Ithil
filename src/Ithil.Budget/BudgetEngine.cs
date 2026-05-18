using Ithil.Core.Enums;
using Ithil.Core.Interfaces;
using LanguageExt;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Ithil.Budget;

/// <summary>
/// Redis-backed implementation of IBudgetEngine.
/// Tracks per-agent daily token usage and enforces configurable limits.
/// Fail-open by default — if Redis is unavailable, budget checks return true.
/// Set <see cref="BudgetEngineOptions.FailurePolicy"/> to <see cref="RedisFailurePolicy.FailClosed"/>
/// to reject requests instead.
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
    /// When Redis is unavailable: returns true if FailOpen, throws if FailClosed.
    /// </summary>
    public async Task<bool> IsWithinBudgetAsync(string agentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var usage = await TryGetUsageAsync(agentId);
        return usage.Match(
            Some: u => u < _options.DefaultDailyTokenLimit,
            None: () => true);
    }

    /// <summary>
    /// Increments the agent's token usage and sets a 48-hour expiry on the key.
    /// When Redis is unavailable: logs and returns silently if FailOpen, throws if FailClosed.
    /// </summary>
    public async Task RecordUsageAsync(string agentId, int tokens, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var key = BudgetKeyFactory.ForToday(agentId);
            await _redis.StringIncrementAsync(key, tokens);
            await _redis.KeyExpireAsync(key, TimeSpan.FromDays(2));
        }
        catch (RedisException ex) when (_options.FailurePolicy == RedisFailurePolicy.FailOpen)
        {
            _logger.LogWarning(ex, "Redis unavailable for agent {AgentId}; failing open on RecordUsage", agentId);
        }
    }

    /// <summary>
    /// Returns the agent's token usage for today. Returns 0 if the key does not exist.
    /// </summary>
    public async Task<int> GetUsageAsync(string agentId)
    {
        var usage = await TryGetUsageAsync(agentId);
        return usage.IfNone(0);
    }

    /// <summary>
    /// Deletes today's usage key, effectively resetting the agent's token count to zero.
    /// When Redis is unavailable: logs and returns silently if FailOpen, throws if FailClosed.
    /// </summary>
    public async Task ResetUsageAsync(string agentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var key = BudgetKeyFactory.ForToday(agentId);
            await _redis.KeyDeleteAsync(key);
        }
        catch (RedisException ex) when (_options.FailurePolicy == RedisFailurePolicy.FailOpen)
        {
            _logger.LogWarning(ex, "Redis unavailable for agent {AgentId}; failing open on ResetUsage", agentId);
        }
    }

    // Reads usage from Redis and wraps it in Option<int>.
    // Returns None on a miss. On Redis failure: returns None (FailOpen) or throws (FailClosed).
    private async Task<Option<int>> TryGetUsageAsync(string agentId)
    {
        try
        {
            var key = BudgetKeyFactory.ForToday(agentId);
            var value = await _redis.StringGetAsync(key);
            return value.IsNull ? Option<int>.None : Option<int>.Some((int)value);
        }
        catch (RedisException ex) when (_options.FailurePolicy == RedisFailurePolicy.FailOpen)
        {
            _logger.LogWarning(ex, "Redis unavailable for agent {AgentId}; failing open", agentId);
            return Option<int>.None;
        }
    }
}
