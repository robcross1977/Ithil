using AwesomeAssertions;
using Ithil.Hosting;

namespace Ithil.Hosting.Tests;

/// <summary>
/// Verifies the ToolEntry → ToolSchemaResponse mapping that MapIthilSchema performs
/// before registering /ithil/schema. Tests here exercise the same projection without
/// needing a live WebApplication.
/// </summary>
public sealed class ToolEntryToSchemaResponseTests
{
    private static ToolSchemaResponse Map(ToolEntry entry) =>
        new(entry.Name, entry.Description, entry.AllowWrite, entry.MaxResponseTokens,
            entry.Category, entry.RequiredScopes, entry.HttpMethod, entry.RoutePattern,
            entry.ParameterSources,
            ToolSchemaMapper.BuildInputSchema(entry.ParameterSources, entry.ParameterTypes));

    [Fact]
    public void Mapping_PreservesScalarFields()
    {
        var entry = new ToolEntry
        {
            Name             = "GetOrderStatus",
            Description      = "Returns order status.",
            AllowWrite       = false,
            MaxResponseTokens = 1500,
            Category         = "Orders",
            HttpMethod       = "GET",
            RoutePattern     = "api/orders/{id}/status"
        };

        var response = Map(entry);

        response.Name.Should().Be("GetOrderStatus");
        response.Description.Should().Be("Returns order status.");
        response.AllowWrite.Should().BeFalse();
        response.MaxResponseTokens.Should().Be(1500);
        response.Category.Should().Be("Orders");
        response.HttpMethod.Should().Be("GET");
        response.RoutePattern.Should().Be("api/orders/{id}/status");
    }

    [Fact]
    public void Mapping_PreservesRequiredScopes()
    {
        var entry = new ToolEntry
        {
            Name           = "CreateOrder",
            RequiredScopes = ["orders:write", "inventory:read"]
        };

        var response = Map(entry);

        response.RequiredScopes.Should().BeEquivalentTo(new[] { "orders:write", "inventory:read" });
    }

    [Fact]
    public void Mapping_BuildsInputSchema_FromParameterSourcesAndTypes()
    {
        var entry = new ToolEntry
        {
            Name = "UpdateQuantity",
            ParameterSources = new() { { "id", "route" }, { "qty", "body" } },
            ParameterTypes   = new() { { "id", "string" }, { "qty", "integer" } }
        };

        var response = Map(entry);

        response.InputSchema.Properties["id"].Type.Should().Be("string");
        response.InputSchema.Properties["qty"].Type.Should().Be("integer");
        response.InputSchema.Required.Should().BeEquivalentTo(new[] { "id", "qty" });
    }

    [Fact]
    public void Mapping_NullCategory_IsPreserved()
    {
        var entry = new ToolEntry { Name = "Ping", Category = null };

        Map(entry).Category.Should().BeNull();
    }
}
