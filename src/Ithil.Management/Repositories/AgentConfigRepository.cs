using System.Collections.Concurrent;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Management.Repositories;

/// <summary>
/// In-memory implementation of IAgentConfigRepository.
/// Suitable for development and testing. Replace with a persistent store in production.
/// </summary>
public class AgentConfigRepository : IAgentConfigRepository
{
    private readonly ConcurrentDictionary<string, AgentConfig> _store = new();

    /// <summary>
    /// Returns the config for the given agent, or None if not registered.
    /// </summary>
    public Task<Option<AgentConfig>> GetAsync(string agentId) =>
        Task.FromResult(_store.TryGetValue(agentId, out var config)
                ? Option<AgentConfig>.Some(config)
                : Option<AgentConfig>.None);

    /// <summary>
    /// Adds or replaces the config for the given agent.
    /// </summary>
    public Task UpsertAsync(AgentConfig config)
    {
        _store[config.AgentId] = config;
        return Task.CompletedTask;
    }
}
