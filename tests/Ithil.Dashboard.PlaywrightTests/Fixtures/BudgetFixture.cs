using Ithil.Management.Models;
using Ithil.Management.Services;
using LanguageExt;
using NSubstitute;

namespace Ithil.Dashboard.PlaywrightTests.Fixtures;

/// <summary>
/// Extends DashboardFixture with a live in-memory agent store and a configurable
/// budget query mock so budget overview tests can control what each gauge displays.
/// </summary>
public sealed class BudgetFixture : DashboardFixture
{
    /// <summary>The in-memory agent store. Call Reset() between tests.</summary>
    public InMemoryAgentManagementService AgentService { get; } = new();

    /// <summary>
    /// The budget query mock. Configure per-test by calling
    /// <c>BudgetService.GetStatusAsync(agentId).Returns(...)</c> before navigating.
    /// </summary>
    public IBudgetQueryService BudgetService { get; } = Substitute.For<IBudgetQueryService>();

    protected override IAgentManagementService CreateAgentService() => AgentService;

    protected override IBudgetQueryService CreateBudgetQueryService() => BudgetService;

    /// <summary>
    /// Pre-configures the budget mock so that <paramref name="agentId"/> returns the
    /// supplied values. Saves tests from constructing <see cref="BudgetStatusResponse"/>
    /// directly every time.
    /// </summary>
    public void SetBudget(string agentId, int tokensUsed, int dailyBudget)
    {
        var percentage = dailyBudget == 0 ? 0.0 : (double)tokensUsed / dailyBudget * 100.0;
        BudgetService.GetStatusAsync(agentId)
            .Returns(Task.FromResult(
                Either<ManagementError, BudgetStatusResponse>.Right(new BudgetStatusResponse
                {
                    AgentId = agentId,
                    TokensUsedToday = tokensUsed,
                    DailyBudget = dailyBudget,
                    PercentageUsed = percentage,
                    ResetsAt = DateTime.UtcNow.Date.AddDays(1),
                })));
    }
}
