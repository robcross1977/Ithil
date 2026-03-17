using System.Text.Json;
using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Mcp;
using LanguageExt;
using NSubstitute;

namespace Ithil.Gateway.Tests.Mcp;

public class McpDispatcherTests
{
    // NSubstitute returns default(Seq<ToolRegistryEntry>) — an empty sequence — for unmocked async methods.
    private readonly McpDispatcher _dispatcher = new(
        Substitute.For<IToolRegistry>(),
        Substitute.For<IHttpClientFactory>(),
        new ToolRegistryOptions(),
        Substitute.For<ISemanticCache>(),
        Substitute.For<ITraceNotifier>()
    );

    // JsonElement has no public constructor — parse from a JSON string to get a typed value.
    private static JsonElement JsonId(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task ReturnsInitializeResponse_WithProtocolVersion()
    {
        JsonRpcRequest request = new()
        {
            Jsonrpc = "2.0",
            Id = JsonId("1"),
            Method = "initialize",
        };

        var response = await _dispatcher.DispatchAsync(request);

        response.Should().NotBeNull();
        response!.Result.Should().NotBeNull();
        response.Result!.ToString().Should().Contain("2024-11-05");
    }

    [Fact]
    public async Task ReturnsNull_ForInitializedNotifications()
    {
        JsonRpcRequest request = new()
        {
            Jsonrpc = "2.0",
            Id = JsonId("1"),
            Method = "notifications/initialized",
        };

        var response = await _dispatcher.DispatchAsync(request);

        response.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsMethodNotFound_ForUnknownMethod()
    {
        JsonRpcRequest request = new()
        {
            Jsonrpc = "2.0",
            Id = JsonId("1"),
            Method = "totally/unknown",
        };

        var response = await _dispatcher.DispatchAsync(request);

        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(-32601);
    }

    [Fact]
    public async Task EchoesRequestId_InResponse()
    {
        JsonRpcRequest request = new()
        {
            Jsonrpc = "2.0",
            Id = JsonId("\"test-123\""),
            Method = "initialize",
        };

        var response = await _dispatcher.DispatchAsync(request);

        response!.Id.GetString().Should().Be("test-123");
    }

    [Fact]
    public void JsonRpcResponse_AlwaysIncludesJsonrpcVersion()
    {
        JsonRpcResponse response = JsonRpcResponse.MethodNotFound(JsonId("1"));

        response.Jsonrpc.Should().Be("2.0");
    }
}
