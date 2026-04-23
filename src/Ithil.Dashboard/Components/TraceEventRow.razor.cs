using Ithil.Core.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ithil.Dashboard.Components;

public partial class TraceEventRow
{
    [Parameter] public AgentTraceEvent Event { get; set; } = default!;

    private Color StatusColor => Event.Status switch
    {
        "success" or "cache-hit" => Color.Success,
        "error" or "blocked" or "denied" => Color.Error,
        "pending" => Color.Info,
        _ => Color.Default
    };

    private string RowClass =>
        Event.Status is "error" or "blocked" or "denied" ? "mud-error-text" : string.Empty;
}
