using FluentAssertions;
using Ithil.Dashboard.Services;
using Ithil.Management.Models;
using Ithil.Management.Services;
using LanguageExt;
using NSubstitute;

namespace Ithil.Dashboard.Tests;

public class BudgetDashboardServiceTests
{
    private readonly IBudgetQueryService _budgetQueryService = Substitute.For<IBudgetQueryService>();

    private BudgetDashboardService CreateService() => new(_budgetQueryService);

    private static BudgetStatusResponse BuildResponse(string agentId, double percentageUsed) => new()
    {
        AgentId         = agentId,
        DailyBudget     = 100_000,
        TokensUsedToday = (int)(100_000 * percentageUsed / 100),
        PercentageUsed  = percentageUsed,
        ResetsAt        = DateTime.UtcNow.Date.AddDays(1)
    };

    [Fact]
    public async Task BudgetDashboardService_ReturnsSome_ForKnownAgent()
    {
        _budgetQueryService.GetStatusAsync("agent-1")
            .Returns(Either<ManagementError, BudgetStatusResponse>.Right(BuildResponse("agent-1", 50.0)));

        var result = await CreateService().GetAsync("agent-1", "Agent One");

        result.IsSome.Should().BeTrue();
        result.IfSome(vm => vm.AgentId.Should().Be("agent-1"));
    }

    [Fact]
    public async Task BudgetDashboardService_ReturnsNone_ForUnknownAgent()
    {
        _budgetQueryService.GetStatusAsync("ghost")
            .Returns(Either<ManagementError, BudgetStatusResponse>.Left(new ManagementError.NotFound("ghost")));

        var result = await CreateService().GetAsync("ghost", "Ghost Agent");

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task BudgetDashboardService_ReturnsWarning_WhenThresholdExceeded()
    {
        _budgetQueryService.GetStatusAsync("agent-1")
            .Returns(Either<ManagementError, BudgetStatusResponse>.Right(BuildResponse("agent-1", 81.0)));

        var result = await CreateService().GetAsync("agent-1", "Agent One");

        result.IfSome(vm => vm.IsWarning.Should().BeTrue());
    }
}
