namespace Ithil.Core.Enums;

/// <summary>
/// Determines how the gateway behaves when Redis is unavailable.
/// </summary>
public enum RedisFailurePolicy
{
    /// <summary>
    /// Requests pass through. Budget enforcement and semantic caching are bypassed.
    /// A warning is logged at startup. Default.
    /// </summary>
    FailOpen,

    /// <summary>
    /// All requests are rejected with 503 until Redis recovers.
    /// Budget governance is never silently bypassed.
    /// </summary>
    FailClosed,
}
