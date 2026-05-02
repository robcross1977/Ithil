using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using System.Collections.Concurrent;

namespace Ithil.Dashboard.PlaywrightTests.Fixtures;

/// <summary>
/// Real fan-out subscription manager for Playwright tests.
/// Unlike a mock, this actually calls registered handlers when <see cref="NotifyAll"/>
/// is invoked, so tests can push live trace events to connected dashboard components.
/// </summary>
public sealed class InMemoryTraceSubscriptionManager : ITraceSubscriptionManager
{
    private readonly ConcurrentDictionary<Action<AgentTraceEvent>, byte> _handlers = new();

    /// <inheritdoc/>
    public void Register(Action<AgentTraceEvent> handler) => _handlers.TryAdd(handler, 0);

    /// <inheritdoc/>
    public void Unregister(Action<AgentTraceEvent> handler) => _handlers.TryRemove(handler, out _);

    /// <inheritdoc/>
    public void NotifyAll(AgentTraceEvent traceEvent)
    {
        foreach (var handler in _handlers.Keys)
            handler(traceEvent);
    }
}
