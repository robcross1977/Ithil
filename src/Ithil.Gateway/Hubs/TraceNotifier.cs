using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Ithil.Gateway.Hubs;

/// <summary>
/// Broadcasts trace events to connected dashboard clients via SignalR and records
/// them in the ring buffer so clients that connect later can receive recent history.
/// </summary>
public class TraceNotifier(IHubContext<TraceHub> hub, ITraceBuffer buffer) : ITraceNotifier
{
    private readonly IHubContext<TraceHub> _hub = hub;
    private readonly ITraceBuffer _buffer = buffer;

    /// <inheritdoc />
    public async Task NotifyAsync(AgentTraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _hub.Clients.Group($"agent:{traceEvent.AgentId}").SendAsync("TraceEvent", traceEvent, cancellationToken),
            _hub.Clients.Group("dashboard-all").SendAsync("TraceEvent", traceEvent, cancellationToken)
        );

        // Write to the ring buffer after broadcasting so a connecting client can receive history.
        _buffer.Add(traceEvent);
    }
}
