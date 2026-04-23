using Ithil.Core.Models;

namespace Ithil.Core.Interfaces;

/// <summary>
/// Managed in-process subscriptions to trace events.
/// </summary>
public interface ITraceSubscriptionManager
{
    /// <summary>    
    /// Registers a callback to be invoked when a trace event is fired.
    /// </summary>
    void Register(Action<AgentTraceEvent> handler);

    /// <summary>
    /// Removes a previously registered callback.
    /// </summary>
    void Unregister(Action<AgentTraceEvent> handler);

    /// <summary>
    /// Invokes all registered callbacks with the given event.
    /// </summary>
    void NotifyAll(AgentTraceEvent traceEvent);
}