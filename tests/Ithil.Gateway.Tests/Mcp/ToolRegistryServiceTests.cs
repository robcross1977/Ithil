using System.Net;
using System.Text;
using FluentAssertions;
using Ithil.Gateway.Mcp;
using NSubstitute;

namespace Ithil.Gateway.Tests.Mcp;

public class ToolRegistryServiceTests
{
    private const string SampleSchemaJson = """
        [
          {
            "name": "GetStock",
            "description": "Gets stock level",
            "allowWrite": false,
            "maxResponseTokens": 2000,
            "category": null,
            "httpMethod": "GET",
            "routePattern": "api/inventory/stock/{sku}",
            "parameterSources": { "sku": "route" },
            "inputSchema": {
              "properties": { "sku": { "type": "string" } },
              "required": ["sku"]
            }
          }
        ]
        """;

    // Creates a ToolRegistryService with an HttpClient backed by the given handler.
    private static ToolRegistryService MakeService(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        ToolRegistryOptions options = new() { DownstreamBaseUrl = "http://localhost:5200" };
        return new ToolRegistryService(factory, options);
    }

    [Fact]
    public async Task GetTools_CallsDownstreamSchemaEndpoint_AndReturnsTools()
    {
        FakeHttpMessageHandler handler = new(SampleSchemaJson);
        var service = MakeService(handler);

        var tools = await service.GetToolsAsync();

        tools.Should().HaveCount(1);
        tools[0].Name.Should().Be("GetStock");
        tools[0].HttpMethod.Should().Be("GET");
        tools[0].RoutePattern.Should().Be("api/inventory/stock/{sku}");
    }

    [Fact]
    public async Task GetTools_CachesResultAfterFirstCall()
    {
        FakeHttpMessageHandler handler = new(SampleSchemaJson);
        var service = MakeService(handler);

        await service.GetToolsAsync();
        await service.GetToolsAsync();

        // Handler should only have been hit once — second call uses the in-memory cache.
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTools_ReturnsEmpty_WhenDownstreamUnreachable()
    {
        FakeHttpMessageHandler handler = new(new HttpRequestException("connection refused"));
        var service = MakeService(handler);

        var tools = await service.GetToolsAsync();

        tools.Should().BeEmpty();
    }

    // Minimal fake HttpMessageHandler for controlling what the HttpClient receives.
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string? _responseJson;
        private readonly Exception? _exception;
        public int CallCount { get; private set; }

        public FakeHttpMessageHandler(string responseJson) => _responseJson = responseJson;

        public FakeHttpMessageHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;

            if (_exception is not null)
                throw _exception;

            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson!, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
