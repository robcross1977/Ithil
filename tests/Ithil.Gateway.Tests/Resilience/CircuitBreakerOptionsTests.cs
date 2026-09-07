using AwesomeAssertions;
using Ithil.Gateway.Resilience;

namespace Ithil.Gateway.Tests.Resilience;

/// <summary>
/// Pins the production defaults for CircuitBreakerOptions.
/// These four values determine when the circuit trips, how long it stays open,
/// and what sample window is used to measure the failure rate.
/// Changing any of them affects circuit behaviour in production — pin them explicitly.
/// </summary>
public sealed class CircuitBreakerOptionsTests
{
    [Fact]
    public void MinimumThroughput_DefaultsTo5()
    {
        new CircuitBreakerOptions().MinimumThroughput.Should().Be(5);
    }

    [Fact]
    public void FailureRatio_DefaultsTo1()
    {
        // 1.0 means every request in the window must fail before the circuit opens.
        new CircuitBreakerOptions().FailureRatio.Should().Be(1.0);
    }

    [Fact]
    public void SamplingDuration_DefaultsTo30Seconds()
    {
        new CircuitBreakerOptions().SamplingDuration.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void BreakDuration_DefaultsTo30Seconds()
    {
        new CircuitBreakerOptions().BreakDuration.Should().Be(TimeSpan.FromSeconds(30));
    }
}
