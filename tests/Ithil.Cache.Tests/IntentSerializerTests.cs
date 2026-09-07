using AwesomeAssertions;
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

    [Fact]
    public void ProducesExactFormat()
    {
        // Pins the "ToolName:{...}" separator and JSON structure so a formatting
        // change doesn't silently break embedding consistency.
        var result = IntentSerializer.Serialize("GetInventory", new { productId = 45 });

        result.Should().Be("GetInventory:{\"productId\":45}");
    }

    [Fact]
    public void EmptyParameters_ProducesEmptyJsonObject()
    {
        var result = IntentSerializer.Serialize("GetInventory", new { });

        result.Should().Be("GetInventory:{}");
    }

    [Fact]
    public void PrimitiveParameter_PassedThroughDirectly()
    {
        // Non-object types bypass key sorting and are emitted verbatim.
        var result = IntentSerializer.Serialize("GetInventory", 42);

        result.Should().Be("GetInventory:42");
    }

    [Fact]
    public void NestedObject_SortsKeysAtAllLevels()
    {
        // Outer keys are sorted alphabetically: address, productId, then warehouseId.
        // Inner address keys are also sorted alphabetically, so city comes before zip.
        var a = IntentSerializer.Serialize("CreateShipment", new
        {
            warehouseId = "UK-01",
            productId = 42,
            address = new { zip = "EC1A", city = "London" }
        });
        var b = IntentSerializer.Serialize("CreateShipment", new
        {
            productId = 42,
            address = new { city = "London", zip = "EC1A" },
            warehouseId = "UK-01"
        });

        a.Should().Be(b);
    }

    [Fact]
    public void NestedObject_ProducesCorrectSortedFormat()
    {
        // Pins the exact output so we can verify inner keys are sorted, not just equal.
        var result = IntentSerializer.Serialize("CreateShipment", new
        {
            address = new { zip = "EC1A", city = "London" }
        });

        result.Should().Be("CreateShipment:{\"address\":{\"city\":\"London\",\"zip\":\"EC1A\"}}");
    }
}

