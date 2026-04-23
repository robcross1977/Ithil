using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Microsoft.Extensions.Options;
using Ithil.Core;

namespace Ithil.Dashboard.Hubs;

/// <summary>
/// Manages a Blazor component's subscription to live trace events.
/// Call Subscribe on page load and Unsubscribe on navigation away.
/// Replays a buffered history immediately on subscribe so the component
/// has data on first render without waiting on new events.
/// </summary>
public class DashboardHubConnection(
    ITraceSubscriptionManager subscriptionManager,
    ITraceBuffer buffer,
    IOptions<TraceOptions> options)
{
    /// <summary>
    /// Registers a callback for live trace events and replays recent history into it.
    /// </summary>
    public void Subscribe(Action<AgentTraceEvent> handler)
    {
        var history = buffer.GetRecent(options.Value.BufferSize);
        foreach (var evt in history)
            handler(evt);
        subscriptionManager.Register(handler);
    }

    /// <summary>
    /// Removes the callback. Call this when the component is disposed.
    /// </summary>
    public void Unsubscribe(Action<AgentTraceEvent> handler) =>
        subscriptionManager.Unregister(handler);
}
