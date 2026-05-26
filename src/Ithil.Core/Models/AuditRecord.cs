namespace Ithil.Core.Models;

/// <summary>
/// An immutable record of a single tool call, written to the audit log after the request completes.
/// </summary>
public record AuditRecord
{
    /// <summary>
    /// ISO 8601 UTC timestamp of when the record was created.
    /// </summary>
    public string Timestamp { get; init; } = string.Empty;

    /// <summary>
    /// The trace ID stamped on the originating request, used for cross-component correlation.
    /// </summary>
    public string TraceId { get; init; } = string.Empty;

    /// <summary>
    /// The ID of the agent that made the request.
    /// </summary>
    public string AgentId { get; init; } = string.Empty;

    /// <summary>
    /// The name of the tool that was called.
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// The raw parameters passed to the tool. May have been PII-scrubbed before storage.
    /// </summary>
    public object? Parameters { get; init; }

    /// <summary>
    /// The outcome of the call (e.g. "success", "error", "blocked").
    /// </summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>
    /// Number of tokens consumed by this request. Null if not applicable (e.g. blocked before execution).
    /// </summary>
    public int? TokensUsed { get; init; }

    /// <summary>
    /// How long the request took in milliseconds. Null if not measured.
    /// </summary>
    public int? LatencyMs { get; init; }

    /// <summary>
    /// True if the response was served from the semantic cache rather than the downstream service.
    /// </summary>
    public bool CacheHit { get; init; }

    /// <summary>
    /// True if PII was detected and redacted from the parameters before storage.
    /// </summary>
    public bool PiiScrubbed { get; init; }

    /// <summary>
    /// Error message if the outcome was "error". Null otherwise.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The operator who performed this action. Populated from the admin JWT sub claim.
    /// Only set for operator-initiated records such as budget resets.
    /// </summary>
    public string? OperatorId { get; init; }
}
