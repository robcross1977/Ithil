using AwesomeAssertions;
using Ithil.Core.Models;
using Ithil.Gateway.Hubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ithil.Gateway.Tests.Hubs;

/// <summary>
/// Tests for TraceSubscriptionManager — the singleton fan-out that delivers trace events
/// to in-process Blazor components via registered Action&lt;AgentTraceEvent&gt; handlers.
/// </summary>
public sealed class TraceSubscriptionManagerTests
{
    private static TraceSubscriptionManager CreateManager() =>
        new(NullLogger<TraceSubscriptionManager>.Instance);

    private static AgentTraceEvent MakeEvent(string traceId = "t1") => new()
    {
        TraceId = traceId,
        AgentId = "agent-1",
        ToolName = "GetStock",
        Status = "success",
        Timestamp = DateTime.UtcNow.ToString("O")
    };

    [Fact]
    public void Register_AddsHandlerToReceiveNotifications()
    {
        var manager = CreateManager();
        AgentTraceEvent? received = null;
        manager.Register(e => received = e);

        var evt = MakeEvent();
        manager.NotifyAll(evt);

        received.Should().BeSameAs(evt);
    }

    [Fact]
    public void Unregister_RemovesHandler_NoLongerReceivesNotifications()
    {
        var manager = CreateManager();
        var callCount = 0;
        void Handler(AgentTraceEvent _) => callCount++;

        manager.Register(Handler);
        manager.Unregister(Handler);
        manager.NotifyAll(MakeEvent());

        callCount.Should().Be(0);
    }

    [Fact]
    public void NotifyAll_CallsAllRegisteredHandlers()
    {
        var manager = CreateManager();
        var callsA = 0;
        var callsB = 0;
        manager.Register(_ => callsA++);
        manager.Register(_ => callsB++);

        manager.NotifyAll(MakeEvent());

        callsA.Should().Be(1);
        callsB.Should().Be(1);
    }

    [Fact]
    public void NotifyAll_ContinuesFanOut_WhenOneHandlerThrows()
    {
        // If handler A throws, handler B must still be called.
        // The manager swallows the exception so one bad subscriber cannot block others.
        var manager = CreateManager();
        var callsB = 0;
        manager.Register(_ => throw new InvalidOperationException("bad handler"));
        manager.Register(_ => callsB++);

        var act = () => manager.NotifyAll(MakeEvent());

        // NotifyAll itself must not propagate the exception
        act.Should().NotThrow();
        callsB.Should().Be(1);
    }

    [Fact]
    public void Register_SameHandlerTwice_CalledOnlyOnce()
    {
        // ConcurrentDictionary.TryAdd is a no-op for duplicate keys,
        // so registering the same delegate twice should not double-fire it.
        var manager = CreateManager();
        var callCount = 0;
        void Handler(AgentTraceEvent _) => callCount++;

        manager.Register(Handler);
        manager.Register(Handler);
        manager.NotifyAll(MakeEvent());

        callCount.Should().Be(1);
    }
}
