using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Management.Models;
using LanguageExt;

namespace Ithil.Management.Services;

/// <summary>
/// Queries current token usage and resets budgets on operator request.
/// Budget resets are audit-logged with the operator's identity.
/// </summary>
public class BudgetQueryService(
    IAgentConfigRepository agentConfigs,
    IBudgetEngine budgetEngine,
    IAuditLogger auditLogger) : IBudgetQueryService
{
    public async Task<Either<ManagementError, BudgetStatusResponse>> GetStatusAsync(string agentId)
    {
        var existing = await agentConfigs.GetAsync(agentId);
        if (existing.IsNone)
            return new ManagementError.NotFound(agentId);

        var config = existing.Match(x => x, () => default!);
        var used   = await budgetEngine.GetUsageAsync(agentId);

        return new BudgetStatusResponse
        {
            AgentId          = agentId,
            TokensUsedToday  = used,
            DailyBudget      = config.DailyTokenBudget,
            PercentageUsed   = config.DailyTokenBudget > 0
                ? Math.Round((double)used / config.DailyTokenBudget * 100, 1)
                : 0,
            ResetsAt = DateTime.UtcNow.Date.AddDays(1),
        };
    }

    public async Task<Either<ManagementError, Unit>> ResetAsync(string agentId, string? operatorId)
    {
        var existing = await agentConfigs.GetAsync(agentId);
        if (existing.IsNone)
            return new ManagementError.NotFound(agentId);

        await budgetEngine.ResetUsageAsync(agentId);
        await auditLogger.WriteAsync(new AuditRecord
        {
            Timestamp  = DateTime.UtcNow.ToString("O"),
            AgentId    = agentId,
            Outcome    = "budget-reset",
            OperatorId = operatorId,
        });

        return Unit.Default;
    }
}
