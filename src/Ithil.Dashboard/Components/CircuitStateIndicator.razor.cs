using Ithil.Dashboard.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ithil.Dashboard.Components;

public partial class CircuitStateIndicator
{
    [Parameter] public CircuitState State { get; set; }

    private Color BadgeColor => State switch
    {
        CircuitState.Closed => Color.Success,
        CircuitState.Open => Color.Error,
        CircuitState.HalfOpen => Color.Warning,
        _ => Color.Default
    };

    private string Label => State switch
    {
        CircuitState.Closed => "Closed",
        CircuitState.Open => "Open",
        CircuitState.HalfOpen => "Half-Open",
        _ => "Unknown"
    };
}
