using Ithil.Core.Models;
using Ithil.Dashboard.Hubs;
using Ithil.Dashboard.Services;
using Microsoft.AspNetCore.Components;

namespace Ithil.Dashboard.Pages;

/// <summary>
/// Displays the current circuit breaker state for every agent/tool pair seen so far.
/// History replay on subscribe pre-populates the table from the trace ring buffer.
/// </summary>
public partial class CircuitBreakers : ComponentBase, IDisposable
{
    [Inject] private DashboardHubConnection Hub { get; set; } = default!;
    [Inject] private CircuitDashboardService Circuits { get; set; } = default!;

    protected override void OnInitialized() => Hub.Subscribe(OnTraceEvent);

    private void OnTraceEvent(AgentTraceEvent traceEvent) =>
        InvokeAsync(() =>
        {
            Circuits.UpdateState(traceEvent);
            StateHasChanged();
        });

    public void Dispose() => Hub.Unsubscribe(OnTraceEvent);
}
