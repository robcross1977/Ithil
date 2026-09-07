using AwesomeAssertions;
using Ithil.Core.Models;
using Ithil.Dashboard.Models;
using Ithil.Dashboard.Services;

namespace Ithil.Dashboard.Tests;

public class CircuitDashboardServiceTests
{
    private static AgentTraceEvent MakeEvent(string circuitState) => new()
    {
        TraceId = Guid.NewGuid().ToString(),
        AgentId = "agent-1",
        ToolName = "GetStock",
        Status = $"circuit-{circuitState}",
        Timestamp = DateTime.UtcNow.ToString("O"),
        CircuitState = circuitState
    };

    [Fact]
    public void CircuitDashboardService_MapsClosedState()
    {
        var service = new CircuitDashboardService();
        service.UpdateState(MakeEvent("closed"));

        var result = service.GetAll().Single();
        result.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void CircuitDashboardService_MapsHalfOpenState()
    {
        var service = new CircuitDashboardService();
        service.UpdateState(MakeEvent("half-open"));

        var result = service.GetAll().Single();
        result.State.Should().Be(CircuitState.HalfOpen);
        result.State.Should().NotBe(CircuitState.Open);
    }

    [Fact]
    public void CircuitDashboardService_MapsOpenState()
    {
        var service = new CircuitDashboardService();
        service.UpdateState(MakeEvent("open"));

        service.GetAll().Single().State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void CircuitDashboardService_GetAll_EmptyInitially()
    {
        var service = new CircuitDashboardService();

        service.GetAll().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void CircuitDashboardService_NoOpForNullCircuitState()
    {
        var service = new CircuitDashboardService();
        service.UpdateState(new AgentTraceEvent
        {
            TraceId = "t1", AgentId = "agent-1", ToolName = "GetStock",
            Status = "success", Timestamp = DateTime.UtcNow.ToString("O"),
            CircuitState = null
        });

        service.GetAll().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void CircuitDashboardService_NoOpForUnknownCircuitState()
    {
        var service = new CircuitDashboardService();
        service.UpdateState(MakeEvent("unknown-state"));

        service.GetAll().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void CircuitDashboardService_OverwritesPreviousStateForSameAgentAndTool()
    {
        var service = new CircuitDashboardService();
        service.UpdateState(MakeEvent("open"));
        service.UpdateState(MakeEvent("closed"));

        var all = service.GetAll();
        all.Should().HaveCount(1);
        all.Single().State.Should().Be(CircuitState.Closed);
    }
}
