namespace Ithil.Gateway.Resilience;

/// <summary>
/// Configures thresholds and timing for the circuit breaker applied to each downstream HTTP client.
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Minimum number of requests in the sampling window before the failure ratio is evaluated.
    /// The circuit opens when this many requests have all failed.
    /// </summary>
    public int MinimumThroughput { get; init; } = 5;

    /// <summary>
    /// Fraction of requests that must fail (0.0-1.0) before the circuit opens.
    /// 1.0 means every requests in the window must fail.
    /// </summary>
    public double FailureRatio { get; init; } = 1.0;

    /// <summary>
    /// The sliding window over which failures are counted.
    /// </summary>
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the circuit stays open before allowing one probe request (half-open state).
    /// </summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}
