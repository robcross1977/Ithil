using Ithil.Core.Models;
using Ithil.Dashboard.Hubs;
using Ithil.Dashboard.Services;
using Microsoft.AspNetCore.Components;

namespace Ithil.Dashboard.Pages;

/// <summary>
/// Live trace feed. Subscribes to DashboardHubConnection on init and unsubscribes on dispose.
/// History is replayed on subscribe so the table is populated immediately.
/// </summary>
public partial class TraceFeed : ComponentBase, IDisposable
{
    [Inject] private DashboardHubConnection Hub { get; set; } = default!;
    [Inject] private TraceFeedService Feed { get; set; } = default!;

    protected string? AgentFilter { get; set; }

    /// <summary>Converts empty input to null so GetIdentified returns all agents.</summary>
    private string? ActiveFilter =>
        string.IsNullOrWhiteSpace(AgentFilter) ? null : AgentFilter;

    protected override void OnInitialized() => Hub.Subscribe(OnTraceEvent);

    private void OnTraceEvent(AgentTraceEvent traceEvent)
    {
        Feed.Add(traceEvent);
        InvokeAsync(StateHasChanged);
    }

    public void Dispose() => Hub.Unsubscribe(OnTraceEvent);
}
