using LanguageExt;

namespace Ithil.Core.Interfaces;

/// <summary>
/// Checks whether an agent is permitted to call a specific tool.
/// </summary>
public interface IToolAllowlistService
{
    /// <summary>
    /// Returns true if the agent is allowed to invoke the named tool.
    /// </summary>
    Task<bool> IsAllowedAsync(string agentId, string toolName);

    /// <summary>
    /// Returns the agent's tool allowlist.
    /// Returns None if the agent has no restrictions (all tools permitted).
    /// Returns Some with names if the agent is restricted to a specific set.
    /// Returns Some with an empty set if the agent is unknown (all tools denied).
    /// </summary>
    Task<Option<Seq<string>>> TryGetToolAllowlistAsync(string agentId);
}
