using AwesomeAssertions;
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
    public async Task Create_ReturnsInvalid_WhenLabelIsWhitespace()
    {
        var result = await CreateService().CreateAsync(
            new CreateAgentRequest { Label = "   ", DailyTokenBudget = 50_000 });

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.Invalid>());
    }

    [Fact]
    public async Task Create_GeneratesAgentId_WithAgtPrefix()
    {
        // agt_ + 8 random bytes as lowercase hex = 4 + 16 = 20 chars
        AgentConfig? stored = null;
        _configs.UpsertAsync(Arg.Do<AgentConfig>(c => stored = c)).Returns(Task.CompletedTask);
        _apiKeys.CreateAsync(Arg.Any<string>()).Returns("ithil_live_key");

        await CreateService().CreateAsync(new CreateAgentRequest { Label = "Agent", DailyTokenBudget = 50_000 });

        stored!.AgentId.Should().StartWith("agt_");
        stored.AgentId.Should().HaveLength(20);
    }

    [Fact]
    public async Task Create_DeletesApiKey_WhenConfigUpsertFails()
    {
        // If persisting the agent config fails, the orphaned API key must be removed.
        const string plainKey = "ithil_live_testkey";
        _apiKeys.CreateAsync(Arg.Any<string>()).Returns(plainKey);
        _configs.UpsertAsync(Arg.Any<AgentConfig>()).Returns(Task.FromException(new Exception("DB down")));

        var act = async () => await CreateService().CreateAsync(
            new CreateAgentRequest { Label = "Finance Agent", DailyTokenBudget = 50_000 });

        await act.Should().ThrowAsync<Exception>();
        await _apiKeys.Received(1).DeleteAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetAll_ReturnsAllAgents()
    {
        var configs = LanguageExt.Seq.create(MakeConfig("a1"), MakeConfig("a2"));
        _configs.GetAllAsync().Returns(configs);

        var result = await CreateService().GetAllAsync();

        result.IsRight.Should().BeTrue();
        result.IfRight(r => r.Should().HaveCount(2));
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenAgentNotFound()
    {
        _configs.GetAsync("unknown").Returns(Option<AgentConfig>.None);

        var result = await CreateService().UpdateAsync("unknown", new UpdateAgentRequest { Label = "New" });

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.NotFound>());
    }

    [Fact]
    public async Task Delete_DeletesApiKey_WhenAgentHasApiKeyHash()
    {
        var config = MakeConfig() with { ApiKeyHash = "sha256hashvalue" };
        _configs.GetAsync("agt_abc123").Returns(Option<AgentConfig>.Some(config));
        _configs.DeleteAsync("agt_abc123").Returns(true);

        var result = await CreateService().DeleteAsync("agt_abc123");

        result.IsRight.Should().BeTrue();
        await _apiKeys.Received(1).DeleteAsync("sha256hashvalue");
    }

    [Fact]
    public async Task Delete_DoesNotDeleteApiKey_WhenHashIsNull()
    {
        // Agent was created without an API key (e.g. JWT-only agent).
        var config = MakeConfig() with { ApiKeyHash = null };
        _configs.GetAsync("agt_abc123").Returns(Option<AgentConfig>.Some(config));
        _configs.DeleteAsync("agt_abc123").Returns(true);

        await CreateService().DeleteAsync("agt_abc123");

        await _apiKeys.DidNotReceive().DeleteAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Create_ReturnsApiKey_OnSuccess()
    {
        _apiKeys.CreateAsync(Arg.Any<string>()).Returns("ithil_live_testkey");
        _configs.UpsertAsync(Arg.Any<AgentConfig>()).Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(new CreateAgentRequest { Label = "Finance Agent", DailyTokenBudget = 50_000 });

        result.IsRight.Should().BeTrue();
        result.IfRight(r => r.ApiKey.Should().Be("ithil_live_testkey"));
    }

    [Fact]
    public async Task Create_StoresHash_NotPlaintext()
    {
        const string plainKey = "ithil_live_testkey";
        _apiKeys.CreateAsync(Arg.Any<string>()).Returns(plainKey);

        AgentConfig? stored = null;
        _configs.UpsertAsync(Arg.Do<AgentConfig>(c => stored = c)).Returns(Task.CompletedTask);

        await CreateService().CreateAsync(new CreateAgentRequest { Label = "Finance Agent", DailyTokenBudget = 50_000 });

        stored.Should().NotBeNull();
        stored!.ApiKeyHash.Should().NotBe(plainKey);
        stored.ApiKeyHash.Should().HaveLength(64); // SHA-256 hex is 64 chars
    }

    [Fact]
    public async Task Create_ReturnsInvalid_WhenBudgetIsZero()
    {
        var result = await CreateService().CreateAsync(
            new CreateAgentRequest { Label = "Finance Agent", DailyTokenBudget = 0 });

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.Invalid>());
    }

    [Fact]
    public async Task Update_ReturnsInvalid_WhenLabelIsBlank()
    {
        var result = await CreateService().UpdateAsync(
            "agt_abc123", new UpdateAgentRequest { Label = "   " });

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.Invalid>());
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
    public async Task Update_ReturnsInvalid_WhenBudgetIsZeroOrNegative()
    {
        var result = await CreateService().UpdateAsync(
            "agt_abc123", new UpdateAgentRequest { DailyTokenBudget = 0 });

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.Invalid>());
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_ForUnknownAgent()
    {
        _configs.GetAsync("unknown").Returns(Option<AgentConfig>.None);

        var result = await CreateService().DeleteAsync("unknown");

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.NotFound>());
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenConfigDeleteFails()
    {
        _configs.GetAsync("agt_abc123").Returns(Option<AgentConfig>.Some(MakeConfig()));
        _configs.DeleteAsync("agt_abc123").Returns(false);

        var result = await CreateService().DeleteAsync("agt_abc123");

        result.IsLeft.Should().BeTrue();
        result.IfLeft(e => e.Should().BeOfType<ManagementError.NotFound>());
        await _apiKeys.DidNotReceive().DeleteAsync(Arg.Any<string>());
    }
}
