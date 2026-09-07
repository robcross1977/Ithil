using AwesomeAssertions;
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

    [Fact]
    public async Task AgentDashboardService_GetAll_DelegatesToManagementService()
    {
        _agentManagementService.GetAllAsync()
            .Returns(Either<ManagementError, Seq<AgentResponse>>.Right(Seq<AgentResponse>.Empty));

        var result = await CreateService().GetAllAsync();

        result.IsRight.Should().BeTrue();
        await _agentManagementService.Received(1).GetAllAsync();
    }

    [Fact]
    public async Task AgentDashboardService_Create_WhenServiceReturnsLeft_ReturnsLeft()
    {
        var request = new CreateAgentRequest { Label = "X", DailyTokenBudget = 1_000 };
        _agentManagementService.CreateAsync(request)
            .Returns(Either<ManagementError, CreateAgentResponse>.Left(
                new ManagementError.Invalid("Label too short")));

        var result = await CreateService().CreateAsync(request);

        result.IsLeft.Should().BeTrue();
    }

    [Fact]
    public async Task AgentDashboardService_Delete_WhenServiceReturnsUnexpectedError_ReturnsSomeError()
    {
        _agentManagementService.DeleteAsync("agent-1")
            .Returns(Either<ManagementError, Unit>.Left(
                new ManagementError.Invalid("Something unexpected")));

        var result = await CreateService().DeleteAsync("agent-1");

        result.IsSome.Should().BeTrue();
    }
}
