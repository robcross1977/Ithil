using FluentAssertions;
using Ithil.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace Ithil.Budget.Tests;

/// <summary>
/// Verifies that BudgetEngine respects RedisFailurePolicy when Redis is unavailable.
/// </summary>
public class BudgetEngineFailurePolicyTests
{
    private readonly IDatabase _redis = Substitute.For<IDatabase>();

    private BudgetEngine CreateEngine(RedisFailurePolicy policy) =>
        new(_redis, new BudgetEngineOptions { DefaultDailyTokenLimit = 50_000, FailurePolicy = policy },
            NullLogger<BudgetEngine>.Instance);

    [Fact]
    public async Task IsWithinBudget_ReturnsTrue_WhenRedisUnavailable_AndFailOpen()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var result = await CreateEngine(RedisFailurePolicy.FailOpen)
            .IsWithinBudgetAsync("agent-01", TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWithinBudget_Throws_WhenRedisUnavailable_AndFailClosed()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var act = () => CreateEngine(RedisFailurePolicy.FailClosed)
            .IsWithinBudgetAsync("agent-01", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<RedisConnectionException>();
    }

    [Fact]
    public async Task GetUsage_ReturnsZero_WhenRedisUnavailable_AndFailOpen()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var result = await CreateEngine(RedisFailurePolicy.FailOpen).GetUsageAsync("agent-01");

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetUsage_Throws_WhenRedisUnavailable_AndFailClosed()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var act = () => CreateEngine(RedisFailurePolicy.FailClosed).GetUsageAsync("agent-01");

        await act.Should().ThrowAsync<RedisConnectionException>();
    }

    [Fact]
    public async Task RecordUsage_CompletesWithoutThrowing_WhenRedisUnavailable_AndFailOpen()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var act = () => CreateEngine(RedisFailurePolicy.FailOpen)
            .RecordUsageAsync("agent-01", 100, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordUsage_Throws_WhenRedisUnavailable_AndFailClosed()
    {
        _redis.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var act = () => CreateEngine(RedisFailurePolicy.FailClosed)
            .RecordUsageAsync("agent-01", 100, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<RedisConnectionException>();
    }

    [Fact]
    public async Task ResetUsage_CompletesWithoutThrowing_WhenRedisUnavailable_AndFailOpen()
    {
        _redis.KeyDeleteAsync(Arg.Any<RedisKey>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var act = () => CreateEngine(RedisFailurePolicy.FailOpen)
            .ResetUsageAsync("agent-01", TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetUsage_Throws_WhenRedisUnavailable_AndFailClosed()
    {
        _redis.KeyDeleteAsync(Arg.Any<RedisKey>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var act = () => CreateEngine(RedisFailurePolicy.FailClosed)
            .ResetUsageAsync("agent-01", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<RedisConnectionException>();
    }
}
