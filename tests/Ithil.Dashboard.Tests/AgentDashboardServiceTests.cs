using FluentAssertions;
using Ithil.Dashboard.Services;
using Ithil.Management.Models;
using Ithil.Management.Services;
using LanguageExt;
using NSubstitute;

namespace Ithil.Dashboard.Tests;

public class AgentDashboardServiceTests
{
    private readonly IAgentManagementService _agentManagementService =
        Substitute.For<IAgentManagementService>();

    private AgentDashboardService CreateService() => new(_agentManagementService);

    [Fact]
    public async Task AgentDashboardService_ReturnsRight_OnSuccessfulCreate()
    {
        var request = new CreateAgentRequest { Label = "Test Agent", DailyTokenBudget = 10_000 };
        var response = new CreateAgentResponse
        {
            AgentId        = "agent-1",
            ApiKey         = "key-abc",
            Label          = "Test Agent",
            DailyTokenBudget = 10_000,
            IsActive       = true
        };

        _agentManagementService.CreateAsync(request)
            .Returns(Either<ManagementError, CreateAgentResponse>.Right(response));

        var result = await CreateService().CreateAsync(request);

        result.IsRight.Should().BeTrue();
        result.IfRight(r => r.AgentId.Should().Be("agent-1"));
    }

    [Fact]
    public async Task AgentDashboardService_ReturnsNone_WhenDeletingUnknownAgent()
    {
        _agentManagementService.DeleteAsync("ghost")
            .Returns(Either<ManagementError, Unit>.Left(new ManagementError.NotFound("ghost")));

        var result = await CreateService().DeleteAsync("ghost");

        result.IsNone.Should().BeTrue();
    }
}
