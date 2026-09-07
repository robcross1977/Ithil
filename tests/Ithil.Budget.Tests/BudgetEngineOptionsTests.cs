using AwesomeAssertions;
using Ithil.Core.Enums;

namespace Ithil.Budget.Tests;

public class BudgetEngineOptionsTests
{
    [Fact]
    public void DefaultDailyTokenLimit_Is100000()
    {
        var options = new BudgetEngineOptions();

        options.DefaultDailyTokenLimit.Should().Be(100_000);
    }

    [Fact]
    public void FailurePolicy_DefaultsToFailOpen()
    {
        var options = new BudgetEngineOptions();

        options.FailurePolicy.Should().Be(RedisFailurePolicy.FailOpen);
    }
}
