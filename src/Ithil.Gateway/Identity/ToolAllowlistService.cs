using Ithil.Core.Interfaces;

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
            Some: c => c.AllowedTools.IsEmpty || c.AllowedTools.Contains(toolName),
            None: () => false);
    }
}
