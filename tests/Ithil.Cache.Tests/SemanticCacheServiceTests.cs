using FluentAssertions;
using Ithil.Cache;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace Ithil.Cache.Tests;

public class SemanticCacheServiceTests
{
    private readonly IEmbeddingService _embedder = Substitute.For<IEmbeddingService>();
    private readonly IDatabase _redis = Substitute.For<IDatabase>();

    // A simple unit vector — direction matters for cosine similarity, not magnitude.
    private static readonly float[] TestVector = [1f, 0f, 0f];

    private SemanticCacheService CreateService(float threshold = 0.95f) =>
        new(_embedder, _redis, new SemanticCacheOptions { SimilarityThreshold = threshold });

    [Fact]
    public async Task TryGet_ReturnsCacheResult_WhenSimilarityAboveThreshold()
    {
        _embedder.EmbedAsync(Arg.Any<string>()).Returns(TestVector);

        // RediSearch COSINE metric returns distance (1 - similarity).
        // Distance 0.03 = similarity 0.97, which is above the 0.95 threshold.
        _redis.ExecuteAsync(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(BuildRedisResult("{\"quantity\":42}", distance: 0.03f));

        var result = await CreateService().TryGetAsync("GetInventory", new { productId = 1 });

        result.IsSome.Should().BeTrue();
        result.IfSome(r => r.Similarity.Should().BeApproximately(0.97f, 0.001f));
    }

    [Fact]
    public async Task TryGet_ReturnsNone_WhenSimilarityBelowThreshold()
    {
        _embedder.EmbedAsync(Arg.Any<string>()).Returns(TestVector);

        // Distance 0.10 = similarity 0.90, which is below the 0.95 threshold.
        _redis.ExecuteAsync(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(BuildRedisResult("{\"quantity\":42}", distance: 0.10f));

        var result = await CreateService().TryGetAsync("GetInventory", new { productId = 1 });

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task TryGet_ReturnsNone_WhenNoResults()
    {
        _embedder.EmbedAsync(Arg.Any<string>()).Returns(TestVector);
        _redis.ExecuteAsync(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(RedisResult.Create(Array.Empty<RedisResult>()));

        var result = await CreateService().TryGetAsync("GetInventory", new { productId = 1 });

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task TryGet_ReturnsNone_WhenEmbeddingServiceThrows()
    {
        _embedder.EmbedAsync(Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("model not loaded"));

        var result = await CreateService().TryGetAsync("GetInventory", new { productId = 1 });

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task TryGet_ReturnsSome_WhenSimilarityExactlyAtThreshold()
    {
        // The guard is `similarity < threshold`, so equality is a hit, not a miss.
        _embedder.EmbedAsync(Arg.Any<string>()).Returns(TestVector);
        _redis.ExecuteAsync(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(BuildRedisResult("{\"quantity\":42}", distance: 0.05f)); // 1 - 0.05 = 0.95

        var result = await CreateService(threshold: 0.95f).TryGetAsync("GetInventory", new { productId = 1 });

        result.IsSome.Should().BeTrue();
    }

    [Fact]
    public async Task TryGet_ReturnsNone_WhenResultCountIsZero()
    {
        // FT.SEARCH can return a structurally valid 3-element array where the count field is 0.
        // This exercises the (long)items[0] == 0 guard specifically.
        _embedder.EmbedAsync(Arg.Any<string>()).Returns(TestVector);
        _redis.ExecuteAsync(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(RedisResult.Create(
            [
                RedisResult.Create(0L),
                RedisResult.Create((RedisKey)""),
                RedisResult.Create(Array.Empty<RedisResult>())
            ]));

        var result = await CreateService().TryGetAsync("GetInventory", new { productId = 1 });

        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_CallsHSetAndExpire_WithCorrectTtl()
    {
        _embedder.EmbedAsync(Arg.Any<string>()).Returns(TestVector);
        _redis.ExecuteAsync(Arg.Any<string>(), Arg.Any<object[]>()).Returns(RedisResult.Create((RedisKey)"OK"));

        var ttl = TimeSpan.FromMinutes(15);
        await CreateService().SetAsync("GetInventory", new { productId = 1 }, new { quantity = 42 }, ttl);

        await _redis.Received(1).ExecuteAsync("HSET", Arg.Any<object[]>());
        await _redis.Received(1).ExecuteAsync("EXPIRE",
            Arg.Is<object[]>(args => args.Length == 2 && (long)args[1] == (long)ttl.TotalSeconds));
    }

    [Fact]
    public async Task SetAsync_DoesNotThrow_WhenRedisThrows()
    {
        // Write failures are always swallowed — a failed cache write must never fail the request.
        _embedder.EmbedAsync(Arg.Any<string>()).Returns(TestVector);
        _redis.ExecuteAsync(Arg.Any<string>(), Arg.Any<object[]>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "down"));

        var act = () => CreateService().SetAsync("GetInventory", new { productId = 1 }, new { quantity = 42 }, TimeSpan.FromMinutes(15));

        await act.Should().NotThrowAsync();
    }

    // Builds a mock FT.SEARCH result with one hit.
    // FT.SEARCH returns: [total_count, key, [field, value, ...]]
    // The "score" field holds the COSINE DISTANCE (1 - similarity) returned by RediSearch.
    private static RedisResult BuildRedisResult(string serializedResponse, float distance)
    {
        var fields = RedisResult.Create(
        [
            RedisResult.Create((RedisKey)"response"),
            RedisResult.Create((RedisKey)serializedResponse),
            RedisResult.Create((RedisKey)"score"),
            RedisResult.Create((RedisKey)distance.ToString("F6"))
        ]);

        return RedisResult.Create(
        [
            RedisResult.Create(1L),
            RedisResult.Create((RedisKey)"cache:key"),
            fields
        ]);
    }
}
