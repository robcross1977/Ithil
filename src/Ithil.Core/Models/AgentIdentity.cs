using LanguageExt;

namespace Ithil.Core.Models;

/// <summary>
/// Represents a verified agent identity extracted from a JWT or API key.
/// </summary>
public record AgentIdentity
{
    /// <summary>
    /// The unique identifier for this agent.
    /// </summary>
    public required string AgentId { get; init; }

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
    /// The scopes this agent has been granted.
    /// </summary>
    public Seq<string> Scopes { get; init; } = Seq<string>.Empty;

    /// <summary>
    /// Whether this agent is currently active. Inactive agents are rejected with 403.
    /// </summary>
    public bool IsActive { get; init; }
}
