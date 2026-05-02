using System.Text.Json;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using StackExchange.Redis;

namespace Ithil.Management.Repositories;

/// <summary>
/// Redis-backed agent configuration store. Persists configs in a Redis Hash at key <c>ithil:agents</c>.
/// </summary>
public class RedisAgentConfigRepository(IConnectionMultiplexer redis) : IAgentConfigRepository
{
    private const string HashKey = "ithil:agents";
    private readonly IDatabase _db = redis.GetDatabase();

    /// <summary>
    /// Returns the config for the given agent, or None if not found.
    /// </summary>
    public async Task<Option<AgentConfig>> GetAsync(string agentId)
    {
        var value = await _db.HashGetAsync(HashKey, agentId);
        return value.HasValue
            ? Option<AgentConfig>.Some(Deserialize(value!))
            : Option<AgentConfig>.None;
    }

    /// <summary>
    /// Returns all registered agent configurations.
    /// </summary>
    public async Task<Seq<AgentConfig>> GetAllAsync()
    {
        var entries = await _db.HashGetAllAsync(HashKey);
        var configs = new List<AgentConfig>();
        foreach (var entry in entries)
        {
            if (!entry.Value.HasValue) continue;
            try { configs.Add(Deserialize(entry.Value!)); }
            catch (JsonException) { continue; }
            catch (InvalidOperationException) { continue; }
        }
        return configs.ToSeq();
    }

    /// <summary>
    /// Creates or updates the configuration for an agent.
    /// </summary>
    public Task UpsertAsync(AgentConfig config) =>
        _db.HashSetAsync(HashKey, config.AgentId, Serialize(config));

    /// <summary>
    /// Deletes the configuration for the given agent.
    /// Returns true if deleted, false if not found.
    /// </summary>
    public async Task<bool> DeleteAsync(string agentId) =>
        await _db.HashDeleteAsync(HashKey, agentId);

    private static string Serialize(AgentConfig config) =>
        JsonSerializer.Serialize(new AgentConfigDto(
            config.AgentId,
            config.ApiKeyHash,
            config.Label,
            config.DailyTokenBudget,
            config.AllowedTools.ToArray(),
            config.Scopes.ToArray(),
            config.IsActive
        ));

    private static AgentConfig Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<AgentConfigDto>(json)
            ?? throw new InvalidOperationException(
                "Failed to deserialize AgentConfig from Redis — stored value may be corrupt.");
        return new AgentConfig
        {
            AgentId = dto.AgentId,
            ApiKeyHash = dto.ApiKeyHash,
            Label = dto.Label,
            DailyTokenBudget = dto.DailyTokenBudget,
            AllowedTools = (dto.AllowedTools ?? []).ToSeq(),
            Scopes = (dto.Scopes ?? []).ToSeq(),
            IsActive = dto.IsActive
        };
    }

    private record AgentConfigDto(
        string AgentId,
        string? ApiKeyHash,
        string Label,
        int DailyTokenBudget,
        string[] AllowedTools,
        string[] Scopes,
        bool IsActive
    );
}
