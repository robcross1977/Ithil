using LanguageExt;

namespace Ithil.Core.Models;

/// <summary>
/// Stored configuration for a registered agent.
/// </summary>
public record AgentConfig
{
    /// <summary>
    /// The unique identifier for this agent.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// SHA-256 hash of the agent's API key. Null if no key has been assigned.
    /// </summary>
    public string? ApiKeyHash { get; init; }

    /// <summary>
    /// Human-readable display name for this agent.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Maximum tokens this agent may consume per day.
    /// </summary>
    public int DailyTokenBudget { get; init; }

    /// <summary>
    /// Tool names this agent is permitted to call.
    /// </summary>
    public Seq<string> AllowedTools { get; init; } = Seq<string>.Empty;

    /// <summary>
    /// OAuth scopes this agent holds.
    /// </summary>
    public Seq<string> Scopes { get; init; } = Seq<string>.Empty;

    /// <summary>
    /// Whether this agent is currently active
    /// </summary>
    public bool IsActive { get; init; }
}
