using Ithil.Core.Interfaces;

namespace Ithil.Dashboard.PlaywrightTests.Fixtures;

/// <summary>
/// Extends DashboardFixture with a seeded trace buffer and a real subscription manager
/// for the Live Trace Feed page tests.
/// </summary>
public sealed class TraceFeedFixture : DashboardFixture
{
    /// <summary>Pre-seed trace events here before navigating to the page.</summary>
    public InMemoryTraceBuffer TraceBuffer { get; } = new();

    /// <summary>Push live events here after the page has loaded.</summary>
    public InMemoryTraceSubscriptionManager SubscriptionManager { get; } = new();

    protected override ITraceBuffer CreateTraceBuffer() => TraceBuffer;
    protected override ITraceSubscriptionManager CreateSubscriptionManager() => SubscriptionManager;
}
