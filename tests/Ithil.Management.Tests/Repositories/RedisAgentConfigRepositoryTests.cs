using AwesomeAssertions;
using Ithil.Core.Models;
using Ithil.Management.Repositories;
using NSubstitute;
using StackExchange.Redis;
using System.Text.Json;

namespace Ithil.Management.Tests.Repositories;

public class RedisAgentConfigRepositoryTests
{
    private readonly IConnectionMultiplexer _multiplexer = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly RedisAgentConfigRepository _repo;

    public RedisAgentConfigRepositoryTests()
    {
        _multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_db);
        _repo = new RedisAgentConfigRepository(_multiplexer);
    }

    [Fact]
    public async Task RedisAgentConfigRepository_Get_ReturnsNone_ForUnknownAgent()
    {
        _db.HashGetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(RedisValue.Null);

        var result = await _repo.GetAsync("unknown");

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task RedisAgentConfigRepository_Upsert_ThenGet_RoundTrips_AllFields()
    {
        var config = BuildConfig("agent-1", "Round Trip Agent");
        RedisValue captured = RedisValue.Null;

        // Capture the JSON written by UpsertAsync so we can return it from GetAsync.
        _db.When(d => d.HashSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<RedisValue>(),
                Arg.Any<When>(), Arg.Any<CommandFlags>()))
            .Do(call => captured = call.ArgAt<RedisValue>(2));

        await _repo.UpsertAsync(config);

        _db.HashGetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(captured);

        var result = await _repo.GetAsync(config.AgentId);

        result.IsSome.Should().BeTrue();
        result.IfSome(c =>
        {
            c.AgentId.Should().Be(config.AgentId);
            c.ApiKeyHash.Should().Be(config.ApiKeyHash);
            c.Label.Should().Be(config.Label);
            c.DailyTokenBudget.Should().Be(config.DailyTokenBudget);
            c.AllowedTools.Should().BeEquivalentTo(config.AllowedTools);
            c.Scopes.Should().BeEquivalentTo(config.Scopes);
            c.IsActive.Should().Be(config.IsActive);
        });
    }

    [Fact]
    public async Task RedisAgentConfigRepository_Upsert_Overwrites_ExistingConfig()
    {
        var first = BuildConfig("agent-1", "First Label");
        var second = first with { Label = "Second Label" };
        RedisValue lastStored = RedisValue.Null;

        _db.When(d => d.HashSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<RedisValue>(),
                Arg.Any<When>(), Arg.Any<CommandFlags>()))
            .Do(call => lastStored = call.ArgAt<RedisValue>(2));

        await _repo.UpsertAsync(first);
        await _repo.UpsertAsync(second);

        _db.HashGetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(lastStored);

        var result = await _repo.GetAsync("agent-1");

        result.IsSome.Should().BeTrue();
        result.IfSome(c => c.Label.Should().Be("Second Label"));
    }

    [Fact]
    public async Task RedisAgentConfigRepository_GetAll_ReturnsAllUpsertedAgents()
    {
        var configs = new[]
        {
            BuildConfig("a1", "Agent One"),
            BuildConfig("a2", "Agent Two"),
            BuildConfig("a3", "Agent Three"),
        };

        _db.HashGetAllAsync(Arg.Any<RedisKey>()).Returns(
            configs.Select(c => new HashEntry(c.AgentId, BuildJson(c))).ToArray());

        var result = await _repo.GetAllAsync();

        result.Should().HaveCount(3);
        result.Select(c => c.AgentId).Should().BeEquivalentTo(["a1", "a2", "a3"]);
    }

    [Fact]
    public async Task RedisAgentConfigRepository_Delete_ReturnsTrue_WhenAgentExists()
    {
        _db.HashDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(true);

        var result = await _repo.DeleteAsync("agent-1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RedisAgentConfigRepository_Delete_ReturnsFalse_WhenAgentNotFound()
    {
        _db.HashDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(false);

        var result = await _repo.DeleteAsync("agent-1");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RedisAgentConfigRepository_GetAll_SkipsCorruptEntries()
    {
        // The repository catches JsonException and InvalidOperationException
        // per entry so one bad Redis value cannot break the whole GetAll call.
        var goodConfig = BuildConfig("agent-good", "Survivor");
        _db.HashGetAllAsync(Arg.Any<RedisKey>()).Returns(new[]
        {
            new HashEntry("agent-good", BuildJson(goodConfig)),
            new HashEntry("agent-bad", "not-valid-json"),
        });

        var result = await _repo.GetAllAsync();

        result.Should().HaveCount(1);
        result.Single().AgentId.Should().Be("agent-good");
    }

    [Fact]
    public async Task RedisAgentConfigRepository_Delete_RemovesAgent_FromGetAll()
    {
        var survivor = BuildConfig("agent-2", "Survivor");

        _db.HashDeleteAsync(Arg.Any<RedisKey>(), (RedisValue)"agent-1").Returns(true);
        _db.HashGetAllAsync(Arg.Any<RedisKey>())
            .Returns([new HashEntry("agent-2", BuildJson(survivor))]);

        await _repo.DeleteAsync("agent-1");
        var all = await _repo.GetAllAsync();

        all.Should().HaveCount(1);
        all.Single().AgentId.Should().Be("agent-2");
    }

    private static AgentConfig BuildConfig(string agentId, string label) => new()
    {
        AgentId = agentId,
        ApiKeyHash = "sha256hash==",
        Label = label,
        DailyTokenBudget = 5000,
        AllowedTools = LanguageExt.Seq.create("tool1", "tool2"),
        Scopes = LanguageExt.Seq.create("read", "write"),
        IsActive = true,
    };

    // Mirrors the serialization shape used by RedisAgentConfigRepository internally
    // so tests can construct valid Redis values without accessing the private DTO.
    private static string BuildJson(AgentConfig c) =>
        JsonSerializer.Serialize(new
        {
            c.AgentId,
            c.ApiKeyHash,
            c.Label,
            c.DailyTokenBudget,
            AllowedTools = c.AllowedTools.ToArray(),
            Scopes = c.Scopes.ToArray(),
            c.IsActive,
        });
}
