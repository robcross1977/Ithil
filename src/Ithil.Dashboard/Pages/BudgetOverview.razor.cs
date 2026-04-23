using Ithil.Dashboard.Models;
using Ithil.Dashboard.Services;
using Ithil.Management.Models;
using LanguageExt;
using Microsoft.AspNetCore.Components;

namespace Ithil.Dashboard.Pages;

/// <summary>
/// Shows a BudgetGauge for every registered agent. Refreshes every 10 seconds.
/// </summary>
public partial class BudgetOverview : ComponentBase, IAsyncDisposable
{
    [Inject] private AgentDashboardService AgentService { get; set; } = default!;
    [Inject] private BudgetDashboardService BudgetService { get; set; } = default!;

    protected List<BudgetViewModel> Budgets { get; private set; } = [];

    private Timer? _timer;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
        _timer = new Timer(_ => InvokeAsync(async () =>
        {
            await RefreshAsync();
            StateHasChanged();
        }), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private async Task RefreshAsync()
    {
        var agentsResult = await AgentService.GetAllAsync();
        var agents = agentsResult.Match(Right: a => a, Left: _ => Seq<AgentResponse>.Empty);

        var budgetOptions = await Task.WhenAll(agents.Select(a => BudgetService.GetAsync(a.AgentId)));
        var results = new List<BudgetViewModel>();
        foreach (var budget in budgetOptions)
            budget.IfSome(vm => results.Add(vm));
        Budgets = results;
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer is not null)
            await _timer.DisposeAsync();
    }
}
