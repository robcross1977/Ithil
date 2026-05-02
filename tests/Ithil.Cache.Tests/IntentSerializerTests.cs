using FluentAssertions;
using Ithil.Cache;

namespace Ithil.Cache.Tests;

public class IntentSerializerTests
{
    [Fact]
    public void ProducesIdenticalString_ForSameParamsInDifferentOrder()
    {
        var a = IntentSerializer.Serialize("GetInventory", new { productId = 45, warehouseId = "UK-01" });
        var b = IntentSerializer.Serialize("GetInventory", new { warehouseId = "UK-01", productId = 45 });

        a.Should().Be(b);
    }

    [Fact]
    public void ProducesDifferentStrings_ForDifferentTools()
    {
        var a = IntentSerializer.Serialize("GetInventory", new { productId = 45 });
        var b = IntentSerializer.Serialize("GetOrders", new { productId = 45 });

        a.Should().NotBe(b);
    }

    [Fact]
    public void ProducesDifferentStrings_ForDifferentParams()
    {
        var a = IntentSerializer.Serialize("GetInventory", new { productId = 42 });
        var b = IntentSerializer.Serialize("GetInventory", new { productId = 99 });

        a.Should().NotBe(b);
    } 
}

