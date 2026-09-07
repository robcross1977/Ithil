using System.Security.Claims;
using AwesomeAssertions;
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
    // HttpMethod must be non-empty so ConfigureSessionAsync's invocableTools filter passes them through.
    private static readonly ToolRegistryEntry GetInventory =
        new("GetInventory", "Gets stock levels", false, 2000, null, Array.Empty<string>(), "GET", "api/inventory", new(), new());

    private static readonly ToolRegistryEntry CreateOrder =
        new("CreateOrder", "Creates an order", false, 2000, null, Array.Empty<string>(), "POST", "api/orders", new(), new());

    private static readonly ToolRegistryEntry DeleteUser =
        new("DeleteUser", "Deletes a user", false, 2000, null, Array.Empty<string>(), "DELETE", "api/users/{id}", new(), new());

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

        // Return an active agent config with no scopes so the IsActive check passes.
        // Only tools with no RequiredScopes will be visible; any scoped tool would be filtered out.
        var agentConfigRepo = Substitute.For<IAgentConfigRepository>();
        agentConfigRepo.GetAsync(Arg.Any<string>()).Returns(LanguageExt.Option<AgentConfig>.Some(
            new AgentConfig { AgentId = agentId, Label = "Test", IsActive = true }));

        var services = new ServiceCollection();
        services.AddSingleton(allowlistService);
        services.AddSingleton(toolRegistry);
        services.AddSingleton(agentConfigRepo);
        services.AddSingleton(httpClientFactory);
        services.AddSingleton(new ToolRegistryOptions { DownstreamBaseUrl = "http://localhost" });
        services.AddSingleton(new ToolCallGovernancePipeline(
            budgetEngine, privacyFilter, tokenCounter, traceIdFactory, traceNotifier, auditLogger));
        services.AddSingleton(semanticCache);

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        // Inject the agent_id claim so ConfigureSessionAsync can find it.
        var identity = new ClaimsIdentity(
            [new Claim("agent_id", agentId)],
            authenticationType: "Test");
        context.User = new ClaimsPrincipal(identity);

        return context;
    }

    [Fact]
    public async Task ConfigureSessionAsync_OnlyRegistersAllowedTools_WhenAllowlistPresent()
    {
        var allowlistService = Substitute.For<IToolAllowlistService>();
        allowlistService.TryGetToolAllowlistAsync("agent-A")
            .Returns(Option<Seq<string>>.Some(
                Seq.create("GetInventory", "CreateOrder")));

        var toolRegistry = Substitute.For<IToolRegistry>();
        toolRegistry.GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Seq.create(GetInventory, CreateOrder, DeleteUser));

        var context = BuildContext(allowlistService, toolRegistry);
        var options = new McpServerOptions();

        await McpSessionConfiguration.ConfigureSessionAsync(context, options, TestContext.Current.CancellationToken);

        options.ToolCollection.Should().NotBeNull();
        options.ToolCollection!.Count.Should().Be(2);
    }

    [Fact]
    public async Task ConfigureSessionAsync_RegistersAllTools_WhenNoAllowlistConfigured()
    {
        var allowlistService = Substitute.For<IToolAllowlistService>();
        allowlistService.TryGetToolAllowlistAsync("agent-A")
            .Returns(Option<Seq<string>>.None);

        var toolRegistry = Substitute.For<IToolRegistry>();
        toolRegistry.GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Seq.create(GetInventory, CreateOrder, DeleteUser));

        var context = BuildContext(allowlistService, toolRegistry);
        var options = new McpServerOptions();

        await McpSessionConfiguration.ConfigureSessionAsync(context, options, TestContext.Current.CancellationToken);

        options.ToolCollection.Should().NotBeNull();
        options.ToolCollection!.Count.Should().Be(3);
    }

    [Fact]
    public async Task ConfigureSessionAsync_RegistersNoTools_WhenAllowlistIsEmpty()
    {
        var allowlistService = Substitute.For<IToolAllowlistService>();
        allowlistService.TryGetToolAllowlistAsync("agent-A")
            .Returns(Option<Seq<string>>.Some(Seq<string>.Empty));

        var toolRegistry = Substitute.For<IToolRegistry>();
        toolRegistry.GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Seq.create(GetInventory, CreateOrder, DeleteUser));

        var context = BuildContext(allowlistService, toolRegistry);
        var options = new McpServerOptions();

        await McpSessionConfiguration.ConfigureSessionAsync(context, options, TestContext.Current.CancellationToken);

        options.ToolCollection.Should().NotBeNull();
        options.ToolCollection!.Count.Should().Be(0);
    }

    [Fact]
    public async Task ConfigureSessionAsync_IsNotCaseSensitive_WhenMatchingAllowlist()
    {
        // Allowlist uses uppercase; tool registry names are mixed case — should still match.
        var allowlistService = Substitute.For<IToolAllowlistService>();
        allowlistService.TryGetToolAllowlistAsync("agent-A")
            .Returns(Option<Seq<string>>.Some(
                Seq.create("GETINVENTORY")));

        var toolRegistry = Substitute.For<IToolRegistry>();
        toolRegistry.GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Seq.create(GetInventory, CreateOrder));

        var context = BuildContext(allowlistService, toolRegistry);
        var options = new McpServerOptions();

        await McpSessionConfiguration.ConfigureSessionAsync(context, options, TestContext.Current.CancellationToken);

        options.ToolCollection!.Count.Should().Be(1);
    }
}
