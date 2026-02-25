using FluentAssertions;
using Ithil.Core.Models;
using Ithil.Gateway.Mcp;

namespace Ithil.Gateway.Tests.Mcp;

public class McpDispatcherTests
{
    private readonly McpDispatcher _dispatcher = new();

    [Fact]
    public async Task ReturnsInitializeResponse_WithProtocolVersion()
    {
        var request = new JsonRpcRequest
        {
            Jsonrpc = "2.0",
            Id = "1",
            Method = "initialize"
        };

        var response = await _dispatcher.DispatchAsync(request);

        response.Should().NotBeNull();
        response!.Result.Should().NotBeNull();
        response.Result!.ToString().Should().Contain("2024-11-05");
    }

    [Fact]
    public async Task ReturnsNull_ForInitializedNotifications()
    {
        var request = new JsonRpcRequest
        {
            Jsonrpc = "2.0",
            Id = "1",
            Method = "notifications/initialized"
        };

        var response = await _dispatcher.DispatchAsync(request);

        response.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsMethodNotFound_ForUnknownMethod()
    {
        var request = new JsonRpcRequest
        {
            Jsonrpc = "2.0",
            Id = "1",
            Method = "totally/unknown"
        };

        var response = await _dispatcher.DispatchAsync(request);

        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(-32601);
    }

    [Fact]
    public async Task EchoesRequestId_InResponse()
    {
        var request = new JsonRpcRequest
        {
            Jsonrpc = "2.0",
            Id = "test-123",
            Method = "initialize"
        };

        var response = await _dispatcher.DispatchAsync(request);

        response!.Id.Should().Be("test-123");
    }

    [Fact]
    public void JsonRpcResponse_AlwaysINcludesJsonrpcVersion()
    {
        var response = JsonRpcResponse.MethodNotFound("1");

        response.Jsonrpc.Should().Be("2.0");
    }
}
