using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Identity;
using LanguageExt;
using NSubstitute;

namespace Ithil.Gateway.Tests.Identity;

public class ToolAllowlistServiceTests
{
    private readonly IAgentConfigRepository _repo = Substitute.For<IAgentConfigRepository>();

    private ToolAllowlistService CreateService() => new(_repo);

    private static AgentConfig ConfigWithTools(string agentId, params string[] tools) => new()
    {
        AgentId = agentId,
        Label = "Test",
        DailyTokenBudget = 1000,
        IsActive = true,
        AllowedTools = tools.ToSeq()
    };

    [Fact]
    public async Task TryGetToolAllowlistAsync_ReturnsNone_WhenAllowedToolsIsEmpty()
    {
        _repo.GetAsync("agent-a").Returns(Option<AgentConfig>.Some(ConfigWithTools("agent-a")));

        var result = await CreateService().TryGetToolAllowlistAsync("agent-a");

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task TryGetToolAllowlistAsync_ReturnsSome_WhenToolsAreRestricted()
    {
        _repo.GetAsync("agent-a").Returns(Option<AgentConfig>.Some(
            ConfigWithTools("agent-a", "GetInventory", "CreateOrder")));

        var result = await CreateService().TryGetToolAllowlistAsync("agent-a");

        result.IsSome.Should().BeTrue();
        result.IfSome(names => names.Should().BeEquivalentTo(["GetInventory", "CreateOrder"]));
    }

    [Fact]
    public async Task TryGetToolAllowlistAsync_ReturnsSomeEmpty_WhenAgentNotFound()
    {
        _repo.GetAsync("unknown").Returns(Option<AgentConfig>.None);

        // Agent not found = deny all (Some with empty set, not None which means allow all)
        var result = await CreateService().TryGetToolAllowlistAsync("unknown");

        result.IsSome.Should().BeTrue();
        result.IfSome(names => names.Should().BeEmpty());
    }
}
