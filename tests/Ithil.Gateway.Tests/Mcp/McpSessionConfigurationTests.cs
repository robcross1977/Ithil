using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Mcp;
using Ithil.Gateway.Transforms;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using NSubstitute;

namespace Ithil.Gateway.Tests.Mcp;

public class McpSessionConfigurationTests
{
    // Three sample tools used across tests.
    // ToolRegistryEntry is a positional record — use the primary constructor with dummy routing fields.
    private static readonly ToolRegistryEntry GetInventory =
        new("GetInventory", "Gets stock levels", false, 2000, null, "", "", new(), new());

    private static readonly ToolRegistryEntry CreateOrder =
        new("CreateOrder", "Creates an order", false, 2000, null, "", "", new(), new());

    private static readonly ToolRegistryEntry DeleteUser =
        new("DeleteUser", "Deletes a user", false, 2000, null, "", "", new(), new());

    // Builds a minimal HttpContext whose RequestServices contains all dependencies
    // that ConfigureSessionAsync will resolve via GetRequiredService.
    private static DefaultHttpContext BuildContext(
        IToolAllowlistService allowlistService,
        IToolRegistry toolRegistry,
        string agentId = "agent-A")
    {
        var budgetEngine      = Substitute.For<IBudgetEngine>();
        var privacyFilter     = Substitute.For<IPrivacyFilter>();
        var tokenCounter      = Substitute.For<ITokenCounter>();
        var traceIdFactory    = Substitute.For<ITraceIdFactory>();
        var traceNotifier     = Substitute.For<ITraceNotifier>();
        var auditLogger       = Substitute.For<IAuditLogger>();
        var semanticCache     = Substitute.For<ISemanticCache>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();

        var services = new ServiceCollection();
        services.AddSingleton(allowlistService);
        services.AddSingleton(toolRegistry);
        services.AddSingleton(httpClientFactory);
        services.AddSingleton(new ToolRegistryOptions { DownstreamBaseUrl = "http://localhost" });
        services.AddSingleton(new ToolCallGovernancePipeline(
            budgetEngine, privacyFilter, tokenCounter, traceIdFactory, traceNotifier, auditLogger));
        services.AddSingleton(semanticCache);

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        // Inject the agent_id claim so ConfigureSessionAsync can find it.
        var identity = new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim("agent_id", agentId)],
            authenticationType: "Test");
        context.User = new System.Security.Claims.ClaimsPrincipal(identity);

        return context;
    }

    [Fact]
    public async Task ConfigureSessionAsync_OnlyRegistersAllowedTools_WhenAllowlistPresent()
    {
        var allowlistService = Substitute.For<IToolAllowlistService>();
        allowlistService.TryGetToolAllowlistAsync("agent-A")
            .Returns(Option<LanguageExt.Seq<string>>.Some(
                LanguageExt.Seq.create("GetInventory", "CreateOrder")));

        var toolRegistry = Substitute.For<IToolRegistry>();
        toolRegistry.GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(LanguageExt.Seq.create(GetInventory, CreateOrder, DeleteUser));

        var context = BuildContext(allowlistService, toolRegistry);
        var options = new McpServerOptions();

        await McpSessionConfiguration.ConfigureSessionAsync(context, options, default);

        options.ToolCollection.Should().NotBeNull();
        options.ToolCollection!.Count.Should().Be(2);
    }

    [Fact]
    public async Task ConfigureSessionAsync_RegistersAllTools_WhenNoAllowlistConfigured()
    {
        var allowlistService = Substitute.For<IToolAllowlistService>();
        allowlistService.TryGetToolAllowlistAsync("agent-A")
            .Returns(Option<LanguageExt.Seq<string>>.None);

        var toolRegistry = Substitute.For<IToolRegistry>();
        toolRegistry.GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(LanguageExt.Seq.create(GetInventory, CreateOrder, DeleteUser));

        var context = BuildContext(allowlistService, toolRegistry);
        var options = new McpServerOptions();

        await McpSessionConfiguration.ConfigureSessionAsync(context, options, default);

        options.ToolCollection.Should().NotBeNull();
        options.ToolCollection!.Count.Should().Be(3);
    }

    [Fact]
    public async Task ConfigureSessionAsync_RegistersNoTools_WhenAllowlistIsEmpty()
    {
        var allowlistService = Substitute.For<IToolAllowlistService>();
        allowlistService.TryGetToolAllowlistAsync("agent-A")
            .Returns(Option<LanguageExt.Seq<string>>.Some(LanguageExt.Seq<string>.Empty));

        var toolRegistry = Substitute.For<IToolRegistry>();
        toolRegistry.GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(LanguageExt.Seq.create(GetInventory, CreateOrder, DeleteUser));

        var context = BuildContext(allowlistService, toolRegistry);
        var options = new McpServerOptions();

        await McpSessionConfiguration.ConfigureSessionAsync(context, options, default);

        options.ToolCollection.Should().NotBeNull();
        options.ToolCollection!.Count.Should().Be(0);
    }

    [Fact]
    public async Task ConfigureSessionAsync_IsNotCaseSensitive_WhenMatchingAllowlist()
    {
        // Allowlist uses uppercase; tool registry names are mixed case — should still match.
        var allowlistService = Substitute.For<IToolAllowlistService>();
        allowlistService.TryGetToolAllowlistAsync("agent-A")
            .Returns(Option<LanguageExt.Seq<string>>.Some(
                LanguageExt.Seq.create("GETINVENTORY")));

        var toolRegistry = Substitute.For<IToolRegistry>();
        toolRegistry.GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(LanguageExt.Seq.create(GetInventory, CreateOrder));

        var context = BuildContext(allowlistService, toolRegistry);
        var options = new McpServerOptions();

        await McpSessionConfiguration.ConfigureSessionAsync(context, options, default);

        options.ToolCollection!.Count.Should().Be(1);
    }
}
