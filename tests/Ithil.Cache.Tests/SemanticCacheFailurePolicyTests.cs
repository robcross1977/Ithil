using AwesomeAssertions;
using Ithil.Cache;
using Ithil.Core.Enums;
using Ithil.Core.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace Ithil.Cache.Tests;

/// <summary>
/// Verifies that SemanticCacheService respects RedisFailurePolicy when Redis is unavailable.
/// Write failures are non-fatal under both policies and are not tested here.
/// </summary>
public class SemanticCacheFailurePolicyTests
{
    private readonly IEmbeddingService _embedder = Substitute.For<IEmbeddingService>();
    private readonly IDatabase _redis = Substitute.For<IDatabase>();

    private SemanticCacheService CreateService(RedisFailurePolicy policy) =>
        new(_embedder, _redis, new SemanticCacheOptions { FailurePolicy = policy });

    [Fact]
    public async Task TryGet_ReturnsNone_WhenRedisUnavailable_AndFailOpen()
    {
        _embedder.EmbedAsync(Arg.Any<string>()).Returns([1f, 0f, 0f]);
        _redis.ExecuteAsync(Arg.Any<string>(), Arg.Any<object[]>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "down"));

        var result = await CreateService(RedisFailurePolicy.FailOpen)
            .TryGetAsync("GetInventory", new { productId = 1 });

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task TryGet_Throws_WhenRedisUnavailable_AndFailClosed()
    {
        _embedder.EmbedAsync(Arg.Any<string>()).Returns([1f, 0f, 0f]);
        _redis.ExecuteAsync(Arg.Any<string>(), Arg.Any<object[]>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "down"));

        var act = () => CreateService(RedisFailurePolicy.FailClosed)
            .TryGetAsync("GetInventory", new { productId = 1 });

        await act.Should().ThrowAsync<RedisConnectionException>();
    }
}
