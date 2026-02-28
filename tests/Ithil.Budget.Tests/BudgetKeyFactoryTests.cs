using FluentAssertions;
using Ithil.Budget;

namespace Ithil.Budget.Tests;

public class BudgetKeyFactoryTests
{
    [Fact]
    public void ForToday_ContainsTodaysUtcDate()
    {
        var key = BudgetKeyFactory.ForToday("agent-01");
        key.Should().Contain(DateTime.UtcNow.ToString("yyyyMMdd"));
    }

    [Fact]
    public void ForToday_ContainsAgentId()
    {
        var key = BudgetKeyFactory.ForToday("agent-01");
        key.Should().Contain("agent-01");
    }

    [Fact]
    public void ForDate_DifferentDate_ProducesDifferentKey()
    {
        var today = BudgetKeyFactory.ForDate("agent-01", DateTime.UtcNow);
        var yesterday = BudgetKeyFactory.ForDate("agent-01", DateTime.UtcNow.AddDays(-1));

        today.Should().NotBe(yesterday);
    }
}
