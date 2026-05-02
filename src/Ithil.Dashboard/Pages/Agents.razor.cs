using Ithil.Dashboard.Services;
using Ithil.Management.Models;
using LanguageExt;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ithil.Dashboard.Pages;

/// <summary>
/// Agent CRUD page. Shows all agents, a create form, and a revoke action per row.
/// The one-time API key is displayed once in a dismissible alert after creation.
/// </summary>
public partial class Agents : ComponentBase
{
    [Inject] private AgentDashboardService AgentService { get; set; } = default!;

    protected MudMessageBox? ConfirmBox { get; set; }
    protected string AgentToDelete { get; set; } = string.Empty;

    protected string NewLabel { get; set; } = string.Empty;
    protected int NewDailyBudget { get; set; } = 10_000;
    protected string? NewApiKey { get; set; }
    protected string? ErrorMessage { get; set; }

    protected List<AgentResponse> AgentList { get; private set; } = [];

    protected override async Task OnInitializedAsync() => await LoadAgentsAsync();

    private async Task LoadAgentsAsync()
    {
        var result = await AgentService.GetAllAsync();
        result.IfRight(agents => AgentList = [.. agents]);
    }

    protected async Task CreateAgentAsync()
    {
        NewApiKey = null;
        ErrorMessage = null;

        var request = new CreateAgentRequest { Label = NewLabel, DailyTokenBudget = NewDailyBudget };
        var result = await AgentService.CreateAsync(request);

        result.Match(
            Right: response =>
            {
                NewApiKey = response.ApiKey;
                NewLabel = string.Empty;
                NewDailyBudget = 10_000;
            },
            Left: error => ErrorMessage = error switch
            {
                ManagementError.Invalid e => $"Invalid: {e.Reason}",
                _ => "An unexpected error occurred."
            }
        );

        await LoadAgentsAsync();
    }

    protected async Task ConfirmDeleteAsync(string agentId)
    {
        AgentToDelete = agentId;
        var confirmed = await ConfirmBox!.ShowAsync();
        if (confirmed == true)
            await DeleteAsync(agentId);
    }

    private async Task DeleteAsync(string agentId)
    {
        var error = await AgentService.DeleteAsync(agentId);
        error.IfSome(e => ErrorMessage = e switch
        {
            ManagementError.Invalid inv => $"Invalid: {inv.Reason}",
            _ => "An unexpected error occurred."
        });
        await LoadAgentsAsync();
    }
}
