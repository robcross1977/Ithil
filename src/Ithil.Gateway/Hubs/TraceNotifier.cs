using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Ithil.Gateway.Hubs;

/// <summary>
/// Broadcasts trace events to connected dashboard clients via SignalR and records
/// them in the ring buffer so clients that connect later can receive recent history.
/// Also fans out to in-process Blazor subscribers via ITraceSubscriptionManager.
/// </summary>
public class TraceNotifier(
    IHubContext<TraceHub> hub,
    ITraceBuffer buffer,
    ITraceSubscriptionManager subscriptionManager) : ITraceNotifier
{
    /// <inheritdoc />
    public async Task NotifyAsync(AgentTraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.WhenAll(
                hub.Clients.Group($"agent:{traceEvent.AgentId}").SendAsync("TraceEvent", traceEvent, cancellationToken),
                hub.Clients.Group("dashboard-all").SendAsync("TraceEvent", traceEvent, cancellationToken)
            );
        }
        finally
        {
            // Write to the ring buffer regardless of broadcast outcome so a connecting client can receive history.
            buffer.Add(traceEvent);
            subscriptionManager.NotifyAll(traceEvent);
        }
    }
}
