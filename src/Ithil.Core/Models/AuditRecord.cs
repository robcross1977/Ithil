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
}
