using AwesomeAssertions;
using Ithil.Hosting;

namespace Ithil.Hosting.Tests;

/// <summary>
/// Tests for ToolSchemaMapper.BuildInputSchema.
/// The mapper is used both by ToolRegistryService (gateway side) and MapIthilSchema
/// (downstream side) to produce the MCP-compatible input schema from ToolEntry parameter data.
/// </summary>
public sealed class ToolSchemaMapperTests
{
    [Fact]
    public void BuildInputSchema_EmptyParameters_ReturnsEmptySchema()
    {
        var schema = ToolSchemaMapper.BuildInputSchema([], null);

        schema.Properties.Should().BeEmpty();
        schema.Required.Should().BeEmpty();
    }

    [Fact]
    public void BuildInputSchema_WithNullTypes_AllPropertiesDefaultToString()
    {
        var sources = new Dictionary<string, string>
        {
            { "orderId", "route" },
            { "status",  "query" }
        };

        var schema = ToolSchemaMapper.BuildInputSchema(sources, null);

        schema.Properties["orderId"].Type.Should().Be("string");
        schema.Properties["status"].Type.Should().Be("string");
    }

    [Fact]
    public void BuildInputSchema_UsesTypeFromDictionary_WhenPresent()
    {
        var sources = new Dictionary<string, string> { { "quantity", "body" } };
        var types   = new Dictionary<string, string> { { "quantity", "integer" } };

        var schema = ToolSchemaMapper.BuildInputSchema(sources, types);

        schema.Properties["quantity"].Type.Should().Be("integer");
    }

    [Fact]
    public void BuildInputSchema_FallsBackToString_WhenKeyMissingFromTypes()
    {
        var sources = new Dictionary<string, string>
        {
            { "name",   "body" },
            { "count",  "body" }
        };
        // Only "count" has a type entry; "name" must fall back to "string"
        var types = new Dictionary<string, string> { { "count", "integer" } };

        var schema = ToolSchemaMapper.BuildInputSchema(sources, types);

        schema.Properties["name"].Type.Should().Be("string");
        schema.Properties["count"].Type.Should().Be("integer");
    }

    [Fact]
    public void BuildInputSchema_RequiredList_ContainsAllParameterNames()
    {
        var sources = new Dictionary<string, string>
        {
            { "id",    "route" },
            { "limit", "query" },
            { "body",  "body"  }
        };

        var schema = ToolSchemaMapper.BuildInputSchema(sources, null);

        schema.Required.Should().BeEquivalentTo(new[] { "id", "limit", "body" });
    }

    [Fact]
    public void BuildInputSchema_PropertyDescriptions_AreNull()
    {
        // ToolEntry does not carry per-parameter descriptions yet — they are always null.
        var sources = new Dictionary<string, string> { { "id", "route" } };

        var schema = ToolSchemaMapper.BuildInputSchema(sources, null);

        schema.Properties["id"].Description.Should().BeNull();
    }
}
