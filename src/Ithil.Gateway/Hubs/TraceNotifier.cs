using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Ithil.Gateway.Hubs;

/// <summary>
/// Broadcasts trace events to connected dashboard clients via SignalR
/// </summary>
public class TraceNotifier(IHubContext<TraceHub> hub) : ITraceNotifier
{
    private readonly IHubContext<TraceHub> _hub = hub;

    /// <inheritdoc />
    public Task NotifyAsync(AgentTraceEvent traceEvent) =>
        Task.WhenAll(
            _hub.Clients.Group($"agent:{traceEvent.AgentId}").SendAsync("TraceEvent", traceEvent),
            _hub.Clients.Group("dashboard-all").SendAsync("TraceEvent", traceEvent)
        );
}
