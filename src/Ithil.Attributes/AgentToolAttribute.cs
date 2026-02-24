namespace Ithil.Attributes;

/// <summary>
/// Marks a controller method or class as an MCP-callable tool
/// The Source Generator reads this attribute at compile time to build the tool manifest.
/// </summary>
/// <param name="Description">Human-readable descrription of what this tool does.</param>

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AgentToolAttribute(string description) : Attribute
{
    /// <summary>
    /// The human-readable description of what this tool does.
    /// </summary>
    public string Description { get; } = description;

    /// <summary>
    /// OAuth scopes the calling agent must have to invoke this tool.
    /// </summary>
    public string[]? RequiredScopes { get; init; }

    /// <summary>
    /// Whether this tool is allowed to perform write operations.
    /// Defaults to false.
    /// </summary>
    public bool AllowWrite { get; init; } = false;

    /// <summary>
    /// Maximum number of tokens this tool is allowed to return.
    /// Defaults to 2000.
    /// </summary>
    public int MaxResponseTokens { get; init; } = 2000;

    /// <summary>
    /// Grouping category for display in the dashboard tool library.
    /// </summary>
    public string? Category { get; init; } 
}
