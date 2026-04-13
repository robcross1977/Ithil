using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Core.Interfaces;

/// <summary>
/// Retrieves and stores agent configuration records.
/// </summary>
public interface IAgentConfigRepository
{
    /// <summary>
    /// Returns the configuration for the given agent, or None if not found.
    /// </summary>
    Task<Option<AgentConfig>> GetAsync(string agentId);

    /// <summary>
    /// Creates or updates the configuration for an agent.
    /// </summary>
    Task UpsertAsync(AgentConfig config);

    /// <summary>
    /// Returns all registered agent configurations
    /// </summary>
    Task<Seq<AgentConfig>> GetAllAsync();

    /// <summary>
    /// Deletes the configuration for a given agent.
    /// Returns true if deleted, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(string agentId);
}
