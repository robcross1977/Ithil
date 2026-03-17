using FluentAssertions;
using Ithil.Core.Models;
using Ithil.Gateway.Hubs;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Ithil.Gateway.Tests.Hubs;

public class TraceHubTests
{
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();

    private TraceHub CreateHub()
    {
        return new() { Groups = _groups, Context = _context };
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

