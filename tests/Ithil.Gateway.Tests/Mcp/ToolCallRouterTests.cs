using AwesomeAssertions;
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
            RequiredScopes: Array.Empty<string>(),
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
        var body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("ABC");
        body.Should().Contain("10");
    }

    [Fact]
    public void ConstrainedRouteParam_IsSubstitutedCorrectly()
    {
        // {id:int} — the constraint suffix must be stripped when substituting the value.
        var tool = MakeTool("GET", "api/posts/{id:int}", new() { { "id", "route" } });
        var request = ToolCallRouter.BuildRequest(tool, "http://localhost:5200", Args("""{"id":"42"}"""));

        request.RequestUri!.ToString().Should().Be("http://localhost:5200/api/posts/42");
    }

    [Fact]
    public async Task MultipleBodyParams_WrappedIntoSingleJsonObject()
    {
        // Expanded body properties (title, userId) must all appear inside one JSON object,
        // not as separate request bodies sent sequentially.
        var tool = MakeTool("POST", "api/posts", new() { { "title", "body" }, { "userId", "body" } });
        var request = ToolCallRouter.BuildRequest(tool, "http://localhost:5200",
            Args("""{"title":"Hello","userId":7}"""));

        request.Content.Should().NotBeNull();
        var body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("title");
        body.Should().Contain("Hello");
        body.Should().Contain("userId");
        body.Should().Contain("7");
    }

    [Fact]
    public void RouteParamNames_ConstrainedToken_ReturnsParamName()
    {
        // RouteParamNames must extract just "id" from "{id:int}", not "id:int".
        var names = ToolCallRouter.RouteParamNames("api/posts/{id:int}").ToList();

        names.Should().ContainSingle().Which.Should().Be("id");
    }
}
