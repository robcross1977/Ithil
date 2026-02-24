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
}
