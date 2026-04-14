namespace Ithil.Core.Models;

public record AuditRecord
{
    public string Timestamp { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;
    public string AgentId { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public object? Parameters { get; init; }
    public string Outcome { get; init; } = string.Empty;

    public int? TokensUsed { get; init; }

    public int? LatencyMs { get; init; }

    public bool CacheHit { get; init; }

    public bool PiiScrubbed { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The operator who performed this action. Populated from the admin JWT sub claim.
    /// Only set for operator-initiated records such as budget resets.
    /// </summary>
    public string? OperatorId { get; init; }
}
