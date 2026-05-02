using FluentAssertions;
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
}
