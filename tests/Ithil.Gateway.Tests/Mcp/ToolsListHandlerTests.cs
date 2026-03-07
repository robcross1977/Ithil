using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Mcp.Handlers;
using LanguageExt;
using NSubstitute;
using System.Text.Json;

namespace Ithil.Gateway.Tests.Mcp;

public class ToolsListHandlerTests
{
    private static JsonRpcRequest MakeRequest() => new()
    {
        Jsonrpc = "2.0",
        Id = JsonDocument.Parse("1").RootElement,
        Method = "tools/list"
    };

    private static ToolRegistryEntry MakeTool(string name, string description) =>
        new(
            Name: name,
            Description: description,
            AllowWrite: false,
            MaxResponseTokens: 2000,
            Category: null,
            HttpMethod: "GET",
            RoutePattern: "api/test",
            ParameterSources: new() { { "id", "route" } },
            InputSchema: new McpInputSchema
            {
                Properties = Map.createRange(new[] { ("id", new JsonSchemaProperty { Type = "string" }) }),
                Required = Seq.create("id")
            });

    [Fact]
    public async Task ToolsList_ReturnsRealTools_WhenRegistryHasEntries()
    {
        var registry = Substitute.For<IToolRegistry>();
        registry.GetToolsAsync().Returns(Seq.create(
            MakeTool("GetStock", "Gets stock level"),
            MakeTool("CreateStock", "Creates stock")));

        var response = await ToolsListHandler.HandleAsync(MakeRequest(), registry);

        response.Error.Should().BeNull();
        var json = JsonSerializer.Serialize(response.Result);
        json.Should().Contain("GetStock");
        json.Should().Contain("CreateStock");
        // Routing fields must not be exposed to the AI
        json.Should().NotContain("httpMethod");
        json.Should().NotContain("routePattern");
    }

    [Fact]
    public async Task ToolsList_ReturnsEmpty_WhenRegistryIsEmpty()
    {
        var registry = Substitute.For<IToolRegistry>();
        registry.GetToolsAsync().Returns(Seq<ToolRegistryEntry>.Empty);

        var response = await ToolsListHandler.HandleAsync(MakeRequest(), registry);

        response.Error.Should().BeNull();
        var json = JsonSerializer.Serialize(response.Result);
        json.Should().Contain("tools");
    }
}
