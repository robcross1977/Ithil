using Ithil.Dashboard.Models;
using Ithil.Management.Services;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Ithil.Dashboard.Services;

/// <summary>
/// Reads per-agent budget state and computes consumption thresholds for display.
/// </summary>
public class BudgetDashboardService(IBudgetQueryService budgetQueryService)
{
    private const double WarningThreshold = 80.0;

    /// <summary>
    /// Returns the budget view model for the given agent, or None if the agent
    /// does not exist.
    /// </summary>
    public async Task<Option<BudgetViewModel>> GetAsync(string agentId, string label)
    {
        var result = await budgetQueryService.GetStatusAsync(agentId);
        return result.Match(
            Right: status => Some(new BudgetViewModel
            {
                AgentId = status.AgentId,
                Label = label,
                DailyBudget = status.DailyBudget,
                TokensUsedToday = status.TokensUsedToday,
                PercentageUsed = status.PercentageUsed,
                IsWarning = status.PercentageUsed >= WarningThreshold
            }),
            Left: _ => None
        );
    }
}
