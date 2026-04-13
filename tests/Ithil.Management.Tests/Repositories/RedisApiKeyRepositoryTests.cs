using FluentAssertions;
using Ithil.Core.Crypto;
using Ithil.Management.Repositories;
using NSubstitute;
using StackExchange.Redis;

namespace Ithil.Management.Tests.Repositories;

public class RedisApiKeyRepositoryTests
{
    private readonly IConnectionMultiplexer _multiplexer = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly RedisApiKeyRepository _repo;

    public RedisApiKeyRepositoryTests()
    {
        _multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_db);
        _repo = new RedisApiKeyRepository(_multiplexer);
    }

    [Fact]
    public async Task RedisApiKeyRepository_Create_ReturnsPlaintextKey_WithCorrectPrefix()
    {
        _db.HashSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<RedisValue>())
           .Returns(true);

        var key = await _repo.CreateAsync("agent-1");

        key.Should().StartWith("ithil_live_");
        key.Should().HaveLength(75);
    }

    [Fact]
    public async Task RedisApiKeyRepository_Create_StoresHash_NotPlaintext()
    {
        RedisValue storedField = default;
        _db.HashSetAsync(Arg.Any<RedisKey>(), Arg.Do<RedisValue>(f => storedField = f), Arg.Any<RedisValue>())
           .Returns(true);

        var plaintext = await _repo.CreateAsync("agent-1");

        ((string)storedField!).Should().Be(ApiKeyHasher.Hash(plaintext));
        ((string)storedField!).Should().NotBe(plaintext);
    }

    [Fact]
    public async Task RedisApiKeyRepository_FindByHash_ReturnsSome_ForKnownHash()
    {
        _db.HashGetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
           .Returns((RedisValue)"agent-1");

        var result = await _repo.FindByHashedKeyAsync("some-hash");

        result.IsSome.Should().BeTrue();
        ((string)result).Should().Be("agent-1");
    }

    [Fact]
    public async Task RedisApiKeyRepository_FindByHash_ReturnsNone_ForUnknownHash()
    {
        _db.HashGetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
           .Returns(RedisValue.Null);

        var result = await _repo.FindByHashedKeyAsync("nonexistent-hash");

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task RedisApiKeyRepository_Delete_RemovesHash()
    {
        _db.HashDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
           .Returns(true);

        await _repo.DeleteAsync("some-hash");

        await _db.Received(1).HashDeleteAsync("ithil:apikeys", (RedisValue)"some-hash");
    }

    [Fact]
    public async Task RedisApiKeyRepository_Delete_IsNoOp_ForUnknownHash()
    {
        _db.HashDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
           .Returns(false);

        var act = async () => await _repo.DeleteAsync("nonexistent-hash");

        await act.Should().NotThrowAsync();
    }
}
