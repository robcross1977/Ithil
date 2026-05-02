using Ithil.Core.Interfaces;

namespace Ithil.Dashboard.PlaywrightTests.Fixtures;

/// <summary>
/// Extends DashboardFixture with a seeded trace buffer and a real subscription manager.
/// Tests use <see cref="TraceBuffer"/> to pre-populate circuit state before navigation,
/// and <see cref="SubscriptionManager"/> to push live events while the page is open.
/// </summary>
public sealed class CircuitBreakerFixture : DashboardFixture
{
    /// <summary>Pre-seed circuit events here before navigating to the page.</summary>
    public InMemoryTraceBuffer TraceBuffer { get; } = new();

    /// <summary>Push live events here after the page has loaded.</summary>
    public InMemoryTraceSubscriptionManager SubscriptionManager { get; } = new();

    protected override ITraceBuffer CreateTraceBuffer() => TraceBuffer;
    protected override ITraceSubscriptionManager CreateSubscriptionManager() => SubscriptionManager;
}
