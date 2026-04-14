using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Hubs;
using Ithil.Gateway.Tracing;
using LanguageExt;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ithil.Gateway.Tests.Tracing;

public class TraceHubTests
{
    private static AgentTraceEvent Event(string id) => new()
    {
        TraceId = "t1", AgentId = id, ToolName = "tool", Status = "success", Timestamp = "2026-01-01T00:00:00Z"
    };

    [Fact]
    public async Task SendsBufferContents_OnConnect()
    {
        var events = new[] { Event("A"), Event("B"), Event("C") }.ToSeq();
        var buffer = Substitute.For<ITraceBuffer>();
        buffer.GetRecent(Arg.Any<int>()).Returns(events);

        var caller = Substitute.For<ISingleClientProxy>();
        var clients = Substitute.For<IHubCallerClients>();
        clients.Caller.Returns(caller);

        var hub = new TraceHub(buffer, Options.Create(new TraceOptions()));
        hub.Clients = clients;

        await hub.OnConnectedAsync();

        await caller.Received(1).SendCoreAsync(
            "TraceHistory",
            Arg.Is<object[]>(args =>
                args.Length == 1 &&
                ((Seq<AgentTraceEvent>)args[0]).SequenceEqual(events)),
            Arg.Any<CancellationToken>()
        );
    }
}
