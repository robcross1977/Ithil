using FluentAssertions;
using Ithil.Core.Models;
using Ithil.Gateway.Mcp;
using LanguageExt;
using System.Text.Json;

namespace Ithil.Gateway.Tests.Mcp;

public class ToolCallRouterTests
{
    // Builds a minimal ToolRegistryEntry for testing. Only sets the fields each test needs.
    private static ToolRegistryEntry MakeTool(
        string httpMethod,
        string routePattern,
        Dictionary<string, string> parameterSources) =>
        new(
            Name: "TestTool",
            Description: "desc",
            AllowWrite: false,
            MaxResponseTokens: 2000,
            Category: null,
            HttpMethod: httpMethod,
            RoutePattern: routePattern,
            ParameterSources: parameterSources,
            InputSchema: new McpInputSchema());

    private static JsonElement Args(string json) =>
        JsonDocument.Parse(json).RootElement;

    [Fact]
    public void RouteParam_IsSubstitutedInPath()
    {
        var tool = MakeTool("GET", "api/inventory/stock/{sku}", new() { { "sku", "route" } });
        var request = ToolCallRouter.BuildRequest(tool, "http://localhost:5200", Args("""{"sku":"ABC-123"}"""));

        request.RequestUri!.ToString().Should().Be("http://localhost:5200/api/inventory/stock/ABC-123");
    }

    [Fact]
    public void QueryParam_IsAppendedToUrl()
    {
        var tool = MakeTool("GET", "api/items", new() { { "filter", "query" } });
        var request = ToolCallRouter.BuildRequest(tool, "http://localhost:5200", Args("""{"filter":"active"}"""));

        request.RequestUri!.ToString().Should().Be("http://localhost:5200/api/items?filter=active");
    }

    [Fact]
    public async Task BodyParam_IsSerializedAsJsonBody()
    {
        var tool = MakeTool("POST", "api/items", new() { { "request", "body" } });
        var request = ToolCallRouter.BuildRequest(tool, "http://localhost:5200",
            Args("""{"request":{"Sku":"ABC","Quantity":10}}"""));

        request.Content.Should().NotBeNull();
        var body = await request.Content!.ReadAsStringAsync();
        body.Should().Contain("ABC");
        body.Should().Contain("10");
    }
}
