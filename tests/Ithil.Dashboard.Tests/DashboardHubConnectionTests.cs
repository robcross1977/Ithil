using AwesomeAssertions;
using Ithil.Core;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Dashboard.Hubs;
using LanguageExt;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ithil.Dashboard.Tests;

public sealed class DashboardHubConnectionTests
{
    private readonly ITraceSubscriptionManager _subscriptionManager =
        Substitute.For<ITraceSubscriptionManager>();
    private readonly ITraceBuffer _buffer =
        Substitute.For<ITraceBuffer>();
    private readonly IOptions<TraceOptions> _options =
        Options.Create(new TraceOptions { BufferSize = 10 });

    private DashboardHubConnection CreateHub() =>
        new(_subscriptionManager, _buffer, _options);

    [Fact]
    public void Subscribe_ReplaysHistoryToHandler_OldestFirst()
    {
        // GetRecent returns events newest-first; Subscribe must reverse before replaying
        // so the handler (e.g. TraceFeedService.Add) receives them oldest-first.
        var ev1 = MakeEvent("trace-1");
        var ev2 = MakeEvent("trace-2");
        var ev3 = MakeEvent("trace-3");
        _buffer.GetRecent(10).Returns(new[] { ev3, ev2, ev1 }.ToSeq());

        var received = new List<AgentTraceEvent>();
        CreateHub().Subscribe(received.Add);

        received.Should().Equal(ev1, ev2, ev3);
    }

    [Fact]
    public void Subscribe_RegistersHandlerWithSubscriptionManager()
    {
        _buffer.GetRecent(10).Returns(Seq<AgentTraceEvent>.Empty);
        Action<AgentTraceEvent> handler = _ => { };

        CreateHub().Subscribe(handler);

        _subscriptionManager.Received(1).Register(handler);
    }

    [Fact]
    public void Subscribe_WhenBufferEmpty_StillRegistersHandler()
    {
        _buffer.GetRecent(10).Returns(Seq<AgentTraceEvent>.Empty);
        var received = new List<AgentTraceEvent>();

        CreateHub().Subscribe(received.Add);

        received.Should().BeEmpty();
        _subscriptionManager.Received(1).Register(Arg.Any<Action<AgentTraceEvent>>());
    }

    [Fact]
    public void Unsubscribe_UnregistersHandlerWithSubscriptionManager()
    {
        Action<AgentTraceEvent> handler = _ => { };

        CreateHub().Unsubscribe(handler);

        _subscriptionManager.Received(1).Unregister(handler);
    }

    private static AgentTraceEvent MakeEvent(string traceId) => new()
    {
        TraceId = traceId,
        AgentId = "agent-1",
        ToolName = "TestTool",
        Status = "success",
        Timestamp = DateTime.UtcNow.ToString("O")
    };
}
