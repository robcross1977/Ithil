using AwesomeAssertions;
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
    public async Task FallsBackToApiKey_WhenJwtFails()
    {
        // JWT resolver returns None; API key resolver succeeds; config is active.
        _jwtResolver.TryResolveAsync(Arg.Any<HttpContext>()).Returns(Option<string>.None);
        _apiKeyResolver.TryResolveAsync(Arg.Any<HttpContext>()).Returns(Option<string>.Some("agent-01"));
        _configRepo.GetAsync("agent-01").Returns(Option<AgentConfig>.Some(ActiveConfig("agent-01")));

        var result = await CreateService().ResolveAgentAsync(new DefaultHttpContext());

        result.IsSome.Should().BeTrue();
        result.IfSome(id => id.AgentId.Should().Be("agent-01"));
    }

    [Fact]
    public async Task ReturnsNone_WhenBothResolversFail()
    {
        _jwtResolver.TryResolveAsync(Arg.Any<HttpContext>()).Returns(Option<string>.None);
        _apiKeyResolver.TryResolveAsync(Arg.Any<HttpContext>()).Returns(Option<string>.None);

        var result = await CreateService().ResolveAgentAsync(new DefaultHttpContext());

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnsNone_WhenAgentConfigNotFound()
    {
        _jwtResolver.TryResolveAsync(Arg.Any<HttpContext>()).Returns(Option<string>.Some("agent-01"));
        _configRepo.GetAsync("agent-01").Returns(Option<AgentConfig>.None);

        var result = await CreateService().ResolveAgentAsync(new DefaultHttpContext());

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task MapsAllowedToolsAndScopesFromConfig()
    {
        var config = ActiveConfig("agent-01") with
        {
            AllowedTools = new[] { "GetInventory", "CreateOrder" }.ToSeq(),
            Scopes = new[] { "inventory:read", "orders:write" }.ToSeq()
        };
        _jwtResolver.TryResolveAsync(Arg.Any<HttpContext>()).Returns(Option<string>.Some("agent-01"));
        _configRepo.GetAsync("agent-01").Returns(Option<AgentConfig>.Some(config));

        var result = await CreateService().ResolveAgentAsync(new DefaultHttpContext());

        result.IfSome(id =>
        {
            id.AllowedTools.Should().BeEquivalentTo(new[] { "GetInventory", "CreateOrder" });
            id.Scopes.Should().BeEquivalentTo(new[] { "inventory:read", "orders:write" });
        });
    }

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
