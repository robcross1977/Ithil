using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Identity;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Ithil.Gateway.Tests.Identity;

public class AgentIdentityServiceTests
{
    private readonly IJwtIdentityResolver _jwtResolver = Substitute.For<IJwtIdentityResolver>();
    private readonly IApiKeyIdentityResolver _apiKeyResolver = Substitute.For<IApiKeyIdentityResolver>();
    private readonly IAgentConfigRepository _configRepo = Substitute.For<IAgentConfigRepository>();

    private AgentIdentityService CreateService() =>
        new(_jwtResolver, _apiKeyResolver, _configRepo);

    private static AgentConfig ActiveConfig(string agentId) => new()
    {
        AgentId = agentId,
        Label = "Test Agent",
        DailyTokenBudget = 50000,
        IsActive = true
    };

    [Fact]
    public async Task ReturnsNone_WhenAgentIsInactive()
    {
        _jwtResolver.TryResolveAsync(Arg.Any<HttpContext>()).Returns(Option<string>.Some("agent-01"));
        _configRepo.GetAsync("agent-01").Returns(Option<AgentConfig>.Some(
            ActiveConfig("agent-01") with { IsActive = false }));

        var result = await CreateService().ResolveAgentAsync(new DefaultHttpContext());

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnsIdentity_WhenEverythingPasses()
    {
        _jwtResolver.TryResolveAsync(Arg.Any<HttpContext>()).Returns(Option<string>.Some("agent-01"));
        _configRepo.GetAsync("agent-01").Returns(Option<AgentConfig>.Some(ActiveConfig("agent-01")));

        var result = await CreateService().ResolveAgentAsync(new DefaultHttpContext());

        result.IsSome.Should().BeTrue();
        result.IfSome(identity =>
        {
            identity.AgentId.Should().Be("agent-01");
            identity.DailyTokenBudget.Should().Be(50000);
        });
    }
}
