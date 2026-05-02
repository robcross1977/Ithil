using Ithil.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Ithil.Core; 

namespace Ithil.Gateway.Hubs;

/// <summary>
/// SignalR hub that dashboard clients connect to for live agent trace events.
/// On connect, sends the ring buffer contents so the client immediately has history.
/// </summary>
public class TraceHub(ITraceBuffer buffer, IOptions<TraceOptions> options) : Hub
{
    private readonly ITraceBuffer _buffer = buffer;
    private readonly TraceOptions _options = options.Value;

    /// <summary>
    /// Sends recent event history to the connecting client, then lets normal live
    /// events take over via SignalR broadcasts.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var recent = _buffer.GetRecent(_options.BufferSize);
        await Clients.Caller.SendAsync("TraceHistory", recent);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Adds the caller to a group for a specific agent's events.
    /// </summary>
    public Task SubscribeToAgent(string agentId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");

    /// <summary>
    /// Adds the caller to the global feed that receives events from all agents.
    /// </summary>
    public Task SubscribeToAll() => Groups.AddToGroupAsync(Context.ConnectionId, "dashboard-all");
}
