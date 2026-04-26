using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Management.Models;
using Ithil.Management.Services;
using LanguageExt;
using NSubstitute;

namespace Ithil.Management.Tests.Services;

public class BudgetQueryServiceTests
{
    private readonly IAgentConfigRepository _configs = Substitute.For<IAgentConfigRepository>();
    private readonly IBudgetEngine _budget = Substitute.For<IBudgetEngine>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();

    private BudgetQueryService CreateService() => new(_configs, _budget, _audit);

    private static AgentConfig MakeConfig(int dailyBudget = 50_000) => new()
    {
        AgentId = "agt_abc123",
        Label = "Test Agent",
        DailyTokenBudget = dailyBudget,
        IsActive = true,
    };

    [Fact]
    public async Task GetStatus_ReturnsCorrectPercentage()
    {
        _configs.GetAsync("agt_abc123").Returns(Option<AgentConfig>.Some(MakeConfig(50_000)));
        _budget.GetUsageAsync("agt_abc123").Returns(23_400);

        var result = await CreateService().GetStatusAsync("agt_abc123");

        result.IsRight.Should().BeTrue();
        result.IfRight(r =>
        {
            r.TokensUsedToday.Should().Be(23_400);
            r.DailyBudget.Should().Be(50_000);
            r.PercentageUsed.Should().BeApproximately(46.8, 0.001);
        });
    }

    [Fact]
    public async Task Reset_CallsBudgetEngine_AndAuditLogger()
    {
        _configs.GetAsync("agt_abc123").Returns(Option<AgentConfig>.Some(MakeConfig()));
        _budget.ResetUsageAsync("agt_abc123", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _audit.WriteAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var result = await CreateService().ResetAsync("agt_abc123", "operator@example.com");

        result.IsRight.Should().BeTrue();
        await _budget.Received(1).ResetUsageAsync("agt_abc123", Arg.Any<CancellationToken>());
        await _audit.Received(1).WriteAsync(Arg.Is<AuditRecord>(r =>
                r.AgentId == "agt_abc123" &&
                r.Outcome == "budget-reset" &&
                r.OperatorId == "operator@example.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reset_ReturnsNotFound_ForUnknownAgent()
    {
        _configs.GetAsync("unknown").Returns(Option<AgentConfig>.None);

        var result = await CreateService().ResetAsync("unknown", null);

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.NotFound>());
    }
}
