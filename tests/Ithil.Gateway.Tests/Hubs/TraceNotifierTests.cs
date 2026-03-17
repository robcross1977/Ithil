using FluentAssertions;
using Ithil.Core.Models;
using Ithil.Gateway.Hubs;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Ithil.Gateway.Tests.Hubs;

public class TraceNotifierTests
{
    private readonly IHubContext<TraceHub> _hub = Substitute.For<IHubContext<TraceHub>>();
    private readonly IClientProxy _clientProxy = Substitute.For<IClientProxy>();

    public TraceNotifierTests()
    {
        _hub.Clients.Group(Arg.Any<string>()).Returns(_clientProxy);
    }

    private TraceNotifier CreateNotifier() => new(_hub);

    private static AgentTraceEvent MakeEvent(string agentId = "claude-prod-01") =>
        new()
        {
            TraceId = "trace-1",
            AgentId = agentId,
            ToolName = "tool-1",
            Status = "success",
            Timestamp = DateTime.UtcNow.ToString("O"),
        };

    [Fact]
    public async Task NotifyAsync_SendsToAgentGroup()
    {
        await CreateNotifier().NotifyAsync(MakeEvent());

        _hub.Clients.Received().Group("agent:claude-prod-01");
    }

    [Fact]
    public async Task NotifyAsync_SendsToGlobalGroup()
    {
        await CreateNotifier().NotifyAsync(MakeEvent());

        _hub.Clients.Received().Group("dashboard-all");
    }

    [Fact]
    public async Task NotifyAsync_SendToBothGroups()
    {
        await CreateNotifier().NotifyAsync(MakeEvent());

        _hub.Clients.Received(2).Group(Arg.Any<string>());
        await _clientProxy
            .Received(2)
            .SendCoreAsync("TraceEvent", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AgentTraceEvent_Timestamp_IsUtcIso8601()
    {
        var evt = new AgentTraceEvent
        {
            TraceId = "t",
            AgentId = "a",
            ToolName = "t",
            Status = "success",
            Timestamp = DateTime.UtcNow.ToString("O"),
        };

        var parsed = DateTimeOffset.Parse(evt.Timestamp);
        parsed.Offset.Should().Be(TimeSpan.Zero);
    }
}
