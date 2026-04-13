using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Management.Models;
using Ithil.Management.Services;
using LanguageExt;
using NSubstitute;

namespace Ithil.Management.Tests.Services;

public class AgentManagementServiceTests
{
    private readonly IAgentConfigRepository _configs = Substitute.For<IAgentConfigRepository>();
    private readonly IApiKeyRepository _apiKeys = Substitute.For<IApiKeyRepository>();

    private AgentManagementService CreateService() => new(_configs, _apiKeys);

    private static AgentConfig MakeConfig(string agentId = "agt_abc123") => new()
    {
        AgentId = agentId,
        Label = "Test Agent",
        DailyTokenBudget = 50_000,
        IsActive = true,
    };

    [Fact]
    public async Task Create_ReturnsApiKey_OnSuccess()
    {
        _apiKeys.CreateAsync(Arg.Any<string>()).Returns("ithil_live_testkey");
        _configs.UpsertAsync(Arg.Any<AgentConfig>()).Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(new CreateAgentRequest { Label = "Finance Agent" });

        result.IsRight.Should().BeTrue();
        result.IfRight(r => r.ApiKey.Should().Be("ithil_live_testkey"));
    }

    [Fact]
    public async Task Create_StoresHash_NotPlaintext()
    {
        const string plainKey = "ithil_live_testkey";
        _apiKeys.CreateAsync(Arg.Any<string>()).Returns(plainKey);

        AgentConfig? stored = null;
        await _configs.UpsertAsync(Arg.Do<AgentConfig>(c => stored = c));

        await CreateService().CreateAsync(new CreateAgentRequest { Label = "Finance Agent" });

        stored.Should().NotBeNull();
        stored!.ApiKeyHash.Should().NotBe(plainKey);
        stored.ApiKeyHash.Should().HaveLength(64); // SHA-256 hex is 64 chars
    }

    [Fact]
    public async Task Get_ReturnsRight_ForKnownAgent()
    {
        _configs.GetAsync("agt_abc123").Returns(Option<AgentConfig>.Some(MakeConfig()));

        var result = await CreateService().GetAsync("agt_abc123");

        result.IsRight.Should().BeTrue();
        result.IfRight(r => r.AgentId.Should().Be("agt_abc123"));
    }

    [Fact]
    public async Task Get_ReturnsNotFound_ForUnknownAgent()
    {
        _configs.GetAsync("unknown").Returns(Option<AgentConfig>.None);

        var result = await CreateService().GetAsync("unknown");

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.NotFound>());
    }

    [Fact]
    public async Task Update_AppliesOnlySuppliedFields()
    {
        var existing = MakeConfig() with { Label = "Original Label", DailyTokenBudget = 50_000 };
        _configs.GetAsync("agt_abc123").Returns(Option<AgentConfig>.Some(existing));
        _configs.UpsertAsync(Arg.Any<AgentConfig>()).Returns(Task.CompletedTask);

        // Only update the budget — label should be unchanged.
        var result = await CreateService().UpdateAsync("agt_abc123",
            new UpdateAgentRequest { DailyTokenBudget = 75_000 });

        result.IsRight.Should().BeTrue();
        result.IfRight(r =>
        {
            r.Label.Should().Be("Original Label");
            r.DailyTokenBudget.Should().Be(75_000);
        });
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_ForUnknownAgent()
    {
        _configs.GetAsync("unknown").Returns(Option<AgentConfig>.None);

        var result = await CreateService().DeleteAsync("unknown");

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.NotFound>());
    }
}
