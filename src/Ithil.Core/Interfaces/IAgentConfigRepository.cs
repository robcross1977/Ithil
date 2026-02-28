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
}
