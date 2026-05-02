using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Hubs;
using LanguageExt;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NSubstitute;
using Ithil.Core;

namespace Ithil.Gateway.Tests.Hubs;

public class TraceHubTests
{
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();
    private readonly ITraceBuffer _buffer = Substitute.For<ITraceBuffer>();

    private TraceHub CreateHub()
    {
        _buffer.GetRecent(Arg.Any<int>()).Returns(Seq<AgentTraceEvent>.Empty);
        var hub = new TraceHub(_buffer, Microsoft.Extensions.Options.Options.Create(new TraceOptions()))
        {
            Groups = _groups,
            Context = _context
        };
        return hub;
    }

    [Fact]
    public async Task SubscribeToAgent_AddsToCorrectGroup()
    {
        _context.ConnectionId.Returns("conn-1");
        await CreateHub().SubscribeToAgent("agent-01");
        await _groups
            .Received()
            .AddToGroupAsync("conn-1", "agent:agent-01", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeToAll_AddsToGlobalGroup()
    {
        _context.ConnectionId.Returns("conn-1");
        await CreateHub().SubscribeToAll();
        await _groups
            .Received()
            .AddToGroupAsync("conn-1", "dashboard-all", Arg.Any<CancellationToken>());
    }
}

