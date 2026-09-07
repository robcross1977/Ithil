using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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
        _redis.StringGetAsync(Arg.Any<RedisKey>()).Returns(RedisValue.Null);

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
    public async Task GetUsage_ReturnsZero_WhenKeyNotFound()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>()).Returns(RedisValue.Null);

        var result = await CreateEngine().GetUsageAsync("agent-01");

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetUsage_ReturnsActualUsage_WhenKeyExists()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>()).Returns((RedisValue)7_500);

        var result = await CreateEngine().GetUsageAsync("agent-01");

        result.Should().Be(7_500);
    }

    [Fact]
    public async Task ResetUsage_DeletesCorrectKey()
    {
        var expectedKey = BudgetKeyFactory.ForToday("agent-01");

        await CreateEngine().ResetUsageAsync("agent-01", TestContext.Current.CancellationToken);

        await _redis.Received(1).KeyDeleteAsync(expectedKey);
    }

    [Fact]
    public async Task IsWithinBudget_ThrowsOperationCanceled_WhenTokenCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CreateEngine().IsWithinBudgetAsync("agent-01", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RecordUsage_ThrowsOperationCanceled_WhenTokenCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CreateEngine().RecordUsageAsync("agent-01", 100, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ResetUsage_ThrowsOperationCanceled_WhenTokenCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => CreateEngine().ResetUsageAsync("agent-01", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

}