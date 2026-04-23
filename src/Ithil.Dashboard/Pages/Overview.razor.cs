using Ithil.Core;
using Ithil.Core.Interfaces;
using Ithil.Dashboard.Services;
using Ithil.Management.Models;
using LanguageExt;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Ithil.Dashboard.Pages;

/// <summary>
/// Aggregate stats page. Polls every 10 seconds via a timer.
/// Uses ITraceBuffer directly (Singleton) so no subscription is needed.
/// </summary>
public partial class Overview : ComponentBase, IAsyncDisposable
{
    [Inject] private AgentDashboardService AgentService { get; set; } = default!;
    [Inject] private BudgetDashboardService BudgetService { get; set; } = default!;
    [Inject] private ITraceBuffer TraceBuffer { get; set; } = default!;
    [Inject] private IOptions<TraceOptions> TraceOptions { get; set; } = default!;

    protected int ActiveAgentCount { get; private set; }
    protected int RecentRequestCount { get; private set; }
    protected long TotalTokensToday { get; private set; }

    private Timer? _timer;
    private int _refreshing;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
        _timer = new Timer(_ =>
        {
            // Guard on the timer thread BEFORE queuing onto the renderer, otherwise
            // multiple ticks can stack InvokeAsync callbacks before the first one flips the flag.
            if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
                return;
            _ = InvokeAsync(async () =>
            {
                try
                {
                    await RefreshAsync();
                    StateHasChanged();
                }
                finally
                {
                    Interlocked.Exchange(ref _refreshing, 0);
                }
            });
        }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private async Task RefreshAsync()
    {
        var agentsResult = await AgentService.GetAllAsync();
        var agents = agentsResult.Match(Right: a => a, Left: _ => Seq<AgentResponse>.Empty);

        ActiveAgentCount = agents.Count(a => a.IsActive);

        var budgetOptions = await Task.WhenAll(agents.Select(a => BudgetService.GetAsync(a.AgentId)));
        long total = 0;
        foreach (var budget in budgetOptions)
            budget.IfSome(vm => total += vm.TokensUsedToday);
        TotalTokensToday = total;

        var cutoff = DateTime.UtcNow.AddSeconds(-60);
        RecentRequestCount = TraceBuffer
            .GetRecent(TraceOptions.Value.BufferSize)
            .Count(e => !string.IsNullOrWhiteSpace(e.AgentId) &&
                        DateTime.TryParse(e.Timestamp, out var ts) && ts >= cutoff);
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer is not null)
            await _timer.DisposeAsync();
    }
}
