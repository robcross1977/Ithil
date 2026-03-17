using Microsoft.AspNetCore.SignalR;

namespace Ithil.Gateway.Hubs;

/// <summary>
/// SignalR hub that dashboard clients connect to for live agent trace events.
/// </summary>
public class TraceHub : Hub
{
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
