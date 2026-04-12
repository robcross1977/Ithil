using System.Collections.Concurrent;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Ithil.Management.Repositories;

/// <summary>
/// In-memory agent configuration store backed bya  concurrent dictionary.
/// All data is lost when the process exits. Use for development or testing only.
/// </summary>
public class InMemoryAgentConfigRepository : IAgentConfigRepository
{
    private readonly ConcurrentDictionary<string, AgentConfig> _store = new();

    /// <summary>
    /// Initialises the repository and logs a startup warning that configs are non-persistent
    /// </summary>
    public InMemoryAgentConfigRepository(ILogger<InMemoryAgentConfigRepository> logger)
    {
        logger.LogWarning(
            "[AgentConfigRepository] Using in-memory agent store - all configuration will be lost on restart."
        );
    }

    /// <summary>
    /// Returns the config for the given agent, or None if not registered.
    /// </summary>
    public Task<Option<AgentConfig>> GetAsync(string agentId) =>
        Task.FromResult(
            _store.TryGetValue(agentId, out var config)
                ? Option<AgentConfig>.Some(config)
                : Option<AgentConfig>.None
        );

    /// <summary>
    /// Adds or replaces the config for the given agent.
    /// </summary>
    public Task UpsertAsync(AgentConfig config)
    {
        _store[config.AgentId] = config;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns all registered agent configurations.
    /// </summary>
    public Task<Seq<AgentConfig>> GetAllAsync() => Task.FromResult(_store.Values.ToSeq());

    /// <summary>
    /// Removes the config for the given agent.
    /// Returns true if removed, false if not found.
    /// </summary>
    public Task<bool> DeleteAsync(string agentId) =>
        Task.FromResult(_store.TryRemove(agentId, out _));
}
