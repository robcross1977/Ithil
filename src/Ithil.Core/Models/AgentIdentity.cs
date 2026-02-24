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
    /// The scopes this agent has been granted.
    /// </summary>
    public Seq<string> Scopes { get; init; } = Seq<string>.Empty;
}
