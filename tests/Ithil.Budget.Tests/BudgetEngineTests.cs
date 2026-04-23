using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace Ithil.Budget.Tests;

public class BudgetEngineTests {
    private readonly IDatabase _redis = Substitute.For<IDatabase>();
    private readonly BudgetEngineOptions _options = new () { DefaultDailyTokenLimit = 50000 };

    private BudgetEngine CreateEngine() =>
        new(_redis, _options, NullLogger<BudgetEngine>.Instance);

    [Fact]
    public async Task IsWithinBudget_ReturnsTrue_WhenNoUsageToday()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)RedisValue.Null);

        var result = await CreateEngine().IsWithinBudgetAsync("agent-01", TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWithinBudget_ReturnsTrue_WhenUnderLimit()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)12400);

        var result = await CreateEngine().IsWithinBudgetAsync("agent-01", TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsWithinBudget_ReturnsFalse_WhenAtLimit()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)50000);

        var result = await CreateEngine().IsWithinBudgetAsync("agent-01", TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsWithinBudget_ReturnsFalse_WhenOverLimit()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)51000);

        var result = await CreateEngine().IsWithinBudgetAsync("agent-01", TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RecordUsage_IncrementsCorrectKey()
    {
        var expectedKey = BudgetKeyFactory.ForToday("agent-01");

        await CreateEngine().RecordUsageAsync("agent-01", 312, TestContext.Current.CancellationToken);

        await _redis.Received(1).StringIncrementAsync(expectedKey, 312);
    }

    [Fact]
    public async Task RecordUsage_SetsExpiry_After48Hours()
    {
        var expectedKey = BudgetKeyFactory.ForToday("agent-01");

        await CreateEngine().RecordUsageAsync("agent-01", 100, TestContext.Current.CancellationToken);

        await _redis.Received(1).KeyExpireAsync(expectedKey, TimeSpan.FromDays(2));
    }

    [Fact]
    public async Task IsWithinBudget_ReturnsZero_WhenKeyNotFound()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)RedisValue.Null);

        var result = await CreateEngine().GetUsageAsync("agent-01");

        result.Should().Be(0);
    }

    [Fact]
    public async Task IsWithinBudget_FailsOpen_WhenRedisThrows()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>())
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down"));

        var result = await CreateEngine().IsWithinBudgetAsync("agent-01", TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

}