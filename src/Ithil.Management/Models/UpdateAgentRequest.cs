namespace Ithil.Management.Models;

/// <summary>
/// Partial update payload — only non-null fields are applied.
/// </summary>
public record UpdateAgentRequest
{
    public string? Label { get; init; }
    public int? DailyTokenBudget { get; init; }
    public List<string>? AllowedTools { get; init; }
    public List<string>? Scopes { get; init; }
    public bool? IsActive { get; init; }
}
