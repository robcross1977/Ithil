using Ithil.Core.Interfaces;
using LanguageExt;

namespace Ithil.Gateway.Identity;

/// <summary>
/// Checks whether an agent is permitted to call a specific tool based on its AgentConfig.AllowedTools list.
/// </summary>
internal class ToolAllowlistService(IAgentConfigRepository agentConfigRepository) : IToolAllowlistService
{
    /// <summary>
    /// Returns true if the agent's AllowedTools list is empty (allow all) or contains the tool name.
    /// Returns false if the agent config cannot be found.
    /// </summary>
    public async Task<bool> IsAllowedAsync(string agentId, string toolName)
    {
        var config = await agentConfigRepository.GetAsync(agentId);

        return config.Match(
            Some: c => c.AllowedTools.IsEmpty
                || c.AllowedTools.Exists(t => string.Equals(t, toolName, StringComparison.OrdinalIgnoreCase)),
            None: () => false);
    }

    /// <summary>
    /// Returns the agent's tool allowlist.
    /// Returns None if the agent has no restrictions (all tools permitted).
    /// Returns Some with names if the agent is restricted to a specific set.
    /// Returns Some with an empty set if the agent is unknown (all tools denied).
    /// </summary>
    public async Task<Option<Seq<string>>> TryGetToolAllowlistAsync(string agentId)
    {
        var config = await agentConfigRepository.GetAsync(agentId);

        return config.Match(
            Some: c => c.AllowedTools.IsEmpty
                ? Option<Seq<string>>.None
                : Option<Seq<string>>.Some(c.AllowedTools),
            None: () => Option<Seq<string>>.Some(Seq<string>.Empty));
    }
}
