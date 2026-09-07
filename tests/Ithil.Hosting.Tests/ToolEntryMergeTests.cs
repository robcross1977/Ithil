using AwesomeAssertions;
using Ithil.Hosting;

namespace Ithil.Hosting.Tests;

/// <summary>
/// Tests for merging compile-time [AgentTool] entries with runtime-discovered
/// minimal-API endpoints.
/// </summary>
public class ToolEntryMergeTests
{
    private static ToolEntry Entry(string name, string verb, string route) => new()
    {
        Name = name,
        Description = $"{name} description",
        HttpMethod = verb,
        RoutePattern = route
    };

    [Fact]
    public void Merge_KeepsBoth_WhenRoutesDiffer()
    {
        var merged = WebApplicationExtensions.Merge(
            [Entry("FromAttribute", "GET", "api/orders")],
            [Entry("FromMinimalApi", "POST", "api/orders")]);

        merged.Select(t => t.Name).Should().BeEquivalentTo(["FromAttribute", "FromMinimalApi"]);
    }

    [Fact]
    public void Merge_PrefersAttribute_WhenVerbAndRouteCollide()
    {
        // A controller action reachable through a mapped route would otherwise appear twice.
        // The attribute is authoritative, so the generated entry wins.
        var merged = WebApplicationExtensions.Merge(
            [Entry("FromAttribute", "GET", "api/orders/{id}")],
            [Entry("FromMinimalApi", "GET", "api/orders/{id}")]);

        merged.Should().ContainSingle();
        merged[0].Name.Should().Be("FromAttribute");
    }

    [Fact]
    public void Merge_TreatsLeadingSlashAsEquivalent()
    {
        // The generator emits templates without a leading slash; RoutePattern.RawText has one.
        // Both must resolve to the same key or every tool would be duplicated.
        var merged = WebApplicationExtensions.Merge(
            [Entry("FromAttribute", "GET", "api/orders")],
            [Entry("FromMinimalApi", "GET", "/api/orders")]);

        merged.Should().ContainSingle();
        merged[0].Name.Should().Be("FromAttribute");
    }

    [Fact]
    public void Merge_IsCaseInsensitiveOnVerbAndRoute()
    {
        var merged = WebApplicationExtensions.Merge(
            [Entry("FromAttribute", "GET", "api/Orders")],
            [Entry("FromMinimalApi", "get", "api/orders")]);

        merged.Should().ContainSingle();
    }

    [Fact]
    public void Merge_ReturnsGeneratedOnly_WhenNothingDiscovered()
    {
        var merged = WebApplicationExtensions.Merge(
            [Entry("FromAttribute", "GET", "api/orders")],
            []);

        merged.Should().ContainSingle();
        merged[0].Name.Should().Be("FromAttribute");
    }

    [Fact]
    public void Merge_ReturnsDiscoveredOnly_WhenNoGeneratedTools()
    {
        // The minimal-API-only app: no controllers, so SchemaRegistry.Tools is empty.
        var merged = WebApplicationExtensions.Merge(
            [],
            [Entry("FromMinimalApi", "GET", "api/orders")]);

        merged.Should().ContainSingle();
        merged[0].Name.Should().Be("FromMinimalApi");
    }

    [Fact]
    public void Merge_DeduplicatesWithinDiscoveredSet()
    {
        var merged = WebApplicationExtensions.Merge(
            [],
            [Entry("First", "GET", "api/orders"), Entry("Second", "GET", "api/orders")]);

        merged.Should().ContainSingle();
        merged[0].Name.Should().Be("First");
    }

    [Fact]
    public void Merge_PreservesOrder_GeneratedBeforeDiscovered()
    {
        var merged = WebApplicationExtensions.Merge(
            [Entry("Gen1", "GET", "a"), Entry("Gen2", "GET", "b")],
            [Entry("Disc1", "GET", "c")]);

        merged.Select(t => t.Name).Should().Equal("Gen1", "Gen2", "Disc1");
    }
}
