using FluentAssertions;

namespace Ithil.Attributes.Tests;

public class AgentToolAttributeTests
{
    [Fact]
    public void DefaultValues()
    {
        var attribute = new AgentToolAttribute("a description");

        attribute.AllowWrite.Should().BeFalse();
        attribute.MaxResponseTokens.Should().Be(2000);
        attribute.RequiredScopes.Should().BeNull();
        attribute.Category.Should().BeNull();
    }

    [Fact]
    public void  DescriptionIsStored()
    {
        const string checkString = "Returns stock levels for a SKU";
        var attribute = new AgentToolAttribute(checkString);

        attribute.Description.Should().Be(checkString);
    }

    [Fact]
    public void AllowWriteCanBeEnabled()
    {
        var attribute = new AgentToolAttribute("a description")
        {
            AllowWrite = true
        };

        attribute.AllowWrite.Should().BeTrue();
    }

    [Fact]
    public void RequiredScopesAreStored()
    {
        var attribute = new AgentToolAttribute("a description")
        {
            RequiredScopes = ["inventory.read", "orders.read"]
        };

        attribute.RequiredScopes.Should().Contain("inventory.read");
        attribute.RequiredScopes.Should().Contain("orders.read");
    }

    [Fact]
    public void CategoryIsStored()
    {
        var attribute = new AgentToolAttribute("a description")
        {
            Category = "Inventory"
        };

        attribute.Category.Should().Be("Inventory");
    }

    [Fact]
    public void MaxResponseTokensCanBeOverriden()
    {
        var attribute = new AgentToolAttribute("a description")
        {
            MaxResponseTokens = 500
        };

        attribute.MaxResponseTokens.Should().Be(500);
    }

    [Fact]
    public void AttributeTargetsAreMethodAndClass()
    {
        var usage = typeof(AgentToolAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.Should().HaveFlag(AttributeTargets.Method);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
    }
}
