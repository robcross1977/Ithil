using Ithil.Core.Models;

namespace Ithil.Core.Interfaces;

/// <summary>
/// Broadcasts trace events to connected dashboard clients.
/// </summary>
public interface ITraceNotifier
{
    /// <summary>
    /// Fires a trace event for a completed request.
    /// </summary>
    Task NotifyAsync(AgentTraceEvent traceEvent);
}
