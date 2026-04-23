using Ithil.Dashboard.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ithil.Dashboard.Components;

public partial class BudgetGauge
{
    [Parameter] public BudgetViewModel ViewModel { get; set; } = default!;

    private Color GaugeColor => ViewModel.IsWarning ? Color.Warning : Color.Success;
}
