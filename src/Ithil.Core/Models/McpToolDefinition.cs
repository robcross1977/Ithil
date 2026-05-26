using LanguageExt;

namespace Ithil.Core.Models;

/// <summary>
/// Represents a single MCP-callable tool as it appears in the tool manifest.
/// Produced by the Source Generator from AgentTool-decorated methods.
/// </summary>
public record McpToolDefinition
{
    /// <summary>
    /// The unique name of the tool, derived from the method name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Human-readable description of what the tool does.
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// Whether this tool is allowed to perform write operations.
    /// </summary>
    public bool AllowWrite { get; init; }
    
    /// <summary>
    /// Maximum number of tokens this tool is allowed to return.
    /// </summary>
    public int MaxResponseTokens { get; init; } = 2000;
    
    /// <summary>
    /// Grouping category for display in the dashboard tool library.
    /// </summary>
    public string? Category { get; init; }
    
    /// <summary>
    /// OAuth scopes the calling agent must have to invoke this tool.
    /// </summary>
    public string[] RequiredScopes { get; init; } = [];

    /// <summary>
    /// The input schema describing parameters this tool accepts. 
    /// </summary>
    public required McpInputSchema InputSchema { get; init; }
}
