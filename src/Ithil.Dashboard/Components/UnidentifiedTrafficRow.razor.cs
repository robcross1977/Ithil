using Ithil.Core.Models;
using Microsoft.AspNetCore.Components;

namespace Ithil.Dashboard.Components;

public partial class UnidentifiedTrafficRow
{
    [Parameter] public AgentTraceEvent Event { get; set; } = default!;
}
