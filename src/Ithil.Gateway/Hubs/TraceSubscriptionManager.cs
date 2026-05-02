using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ithil.Gateway.Hubs;


/// <summary>
/// Thread-safe fan-out of trace events to in-process Blazor dashboard components.
/// Registered as a singleton - one instance serves all connected dashboard clients.
/// </summary>
public class TraceSubscriptionManager(ILogger<TraceSubscriptionManager> logger) : ITraceSubscriptionManager
{
    private readonly ConcurrentDictionary<Action<AgentTraceEvent>, byte> _handlers = new();

    /// <inheritdoc/>
    public void Register(Action<AgentTraceEvent> handler)
    {
        _handlers.TryAdd(handler, 0);
    }

    /// <inheritdoc/>
    public void Unregister(Action<AgentTraceEvent> handler)
    {
        _handlers.TryRemove(handler, out _);
    }

    /// <inheritdoc/>
    public void NotifyAll(AgentTraceEvent traceEvent)
    {
        foreach (var handler in _handlers.Keys)
        {
            try
            {
                handler(traceEvent);
            }
            catch (Exception ex)
            {
                // Swallow per-handler failures so one bad subscriber can't block fan-out to the rest.
                logger.LogWarning(ex, "[TraceSubscriptionManager] Handler threw while receiving trace event; continuing with remaining handlers.");
            }
        }
    }
}
