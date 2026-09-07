using AwesomeAssertions;

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

    [Fact]
    public void ForDate_ProducesExactFormat()
    {
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var key = BudgetKeyFactory.ForDate("agent-01", date);

        key.Should().Be("budget:agent-01:20240115");
    }

    [Fact]
    public void ForDate_DifferentAgents_SameDate_ProduceDifferentKeys()
    {
        var date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var keyA = BudgetKeyFactory.ForDate("agent-01", date);
        var keyB = BudgetKeyFactory.ForDate("agent-02", date);

        keyA.Should().NotBe(keyB);
    }
}