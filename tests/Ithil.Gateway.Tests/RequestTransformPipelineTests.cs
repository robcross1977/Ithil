using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Transforms;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Ithil.Gateway.Tests;

public class RequestTransformPipelineTests
{
    private readonly IAgentIdentityService _identityService =
        Substitute.For<IAgentIdentityService>();
    private readonly IBudgetEngine _budgetEngine = Substitute.For<IBudgetEngine>();
    private readonly IToolAllowlistService _allowListService =
        Substitute.For<IToolAllowlistService>();
    private readonly IToolRegistry _toolRegistry = Substitute.For<IToolRegistry>();
    private readonly ITraceIdFactory _traceIdFactory = Substitute.For<ITraceIdFactory>();
    private readonly ITraceNotifier _traceNotifier = Substitute.For<ITraceNotifier>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private RequestTransformPipeline CreatePipeline() =>
        new(_identityService, _budgetEngine, _allowListService, _toolRegistry, _traceIdFactory, _traceNotifier, _auditLogger);

    [Fact]
    public async Task ReturnsUnauthorized_WhenAgentNotResolved()
    {
        _identityService
            .ResolveAgentAsync(Arg.Any<HttpContext>())
            .Returns(Option<AgentIdentity>.None);

        var pipeline = CreatePipeline();
        DefaultHttpContext context = new();

        await pipeline.TransformAsync(context);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task ReturnsTooManyRequests_WhenBudgetExceeded()
    {
        const string agentId = "agent-1";
        AgentIdentity identity = new() { AgentId = agentId, Label = "test-agent" };
        _identityService
            .ResolveAgentAsync(Arg.Any<HttpContext>())
            .Returns(Option<AgentIdentity>.Some(identity));
        _budgetEngine.IsWithinBudgetAsync(agentId, Arg.Any<CancellationToken>()).Returns(false);

        var pipeline = CreatePipeline();
        DefaultHttpContext context = new();

        await pipeline.TransformAsync(context);

        context.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task ReturnsForbidden_WhenToolNotAllowed()
    {
        const string agentId = "agent-1";
        AgentIdentity identity = new() { AgentId = agentId, Label = "test-agent" };
        _identityService
            .ResolveAgentAsync(Arg.Any<HttpContext>())
            .Returns(Option<AgentIdentity>.Some(identity));
        _budgetEngine.IsWithinBudgetAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);
        _allowListService.IsAllowedAsync(agentId, Arg.Any<string>()).Returns(false);

        var pipeline = CreatePipeline();
        DefaultHttpContext context = new();
        context.Request.Path = "/tools/GetInventory";

        await pipeline.TransformAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ReturnsForbidden_WhenAgentLacksRequiredScope()
    {
        const string agentId = "agent-1";
        // Agent has no scopes; the tool requires "inventory:read".
        AgentIdentity identity = new() { AgentId = agentId, Label = "test-agent", Scopes = LanguageExt.Seq<string>.Empty };
        _identityService
            .ResolveAgentAsync(Arg.Any<HttpContext>())
            .Returns(Option<AgentIdentity>.Some(identity));
        _budgetEngine.IsWithinBudgetAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);
        _allowListService.IsAllowedAsync(agentId, Arg.Any<string>()).Returns(true);

        // Registry returns a tool that requires "inventory:read".
        var scopedTool = new ToolRegistryEntry(
            "GetInventory", "desc", false, 2000, null,
            new[] { "inventory:read" },
            "GET", "api/inventory", new(), new());
        _toolRegistry
            .GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(LanguageExt.Seq.create(scopedTool));

        var pipeline = CreatePipeline();
        DefaultHttpContext context = new();
        context.Request.Path = "/tools/GetInventory";

        await pipeline.TransformAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ReturnsOk_WhenAgentScopesMatchCaseInsensitively()
    {
        const string agentId = "agent-1";
        // Agent has scope in uppercase; tool declares it in lowercase — should still pass.
        AgentIdentity identity = new()
        {
            AgentId = agentId, Label = "test-agent",
            Scopes = new[] { "INVENTORY:READ" }.ToSeq(),
        };
        _identityService
            .ResolveAgentAsync(Arg.Any<HttpContext>())
            .Returns(Option<AgentIdentity>.Some(identity));
        _budgetEngine.IsWithinBudgetAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);
        _allowListService.IsAllowedAsync(agentId, Arg.Any<string>()).Returns(true);
        _traceIdFactory.Create().Returns("trace-xyz");

        var scopedTool = new ToolRegistryEntry(
            "GetInventory", "desc", false, 2000, null,
            new[] { "inventory:read" },
            "GET", "api/inventory", new(), new());
        _toolRegistry
            .GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(LanguageExt.Seq.create(scopedTool));

        var pipeline = CreatePipeline();
        DefaultHttpContext context = new();
        context.Request.Path = "/tools/GetInventory";

        await pipeline.TransformAsync(context);

        // Case-insensitive match — should proceed past scope check.
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task StampsTraceIdHeader_WhenAllChecksPass()
    {
        const string agentId = "agent-1";
        const string traceId = "trace-abc-123";

        AgentIdentity identity = new() { AgentId = agentId, Label = "test-agent" };
        _identityService
            .ResolveAgentAsync(Arg.Any<HttpContext>())
            .Returns(Option<AgentIdentity>.Some(identity));
        _budgetEngine.IsWithinBudgetAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);
        _allowListService.IsAllowedAsync(agentId, Arg.Any<string>()).Returns(true);
        _traceIdFactory.Create().Returns(traceId);
        // Tool has no required scopes — scope check passes without restriction.
        _toolRegistry
            .GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(LanguageExt.Seq<ToolRegistryEntry>.Empty);

        var pipeline = CreatePipeline();
        DefaultHttpContext context = new();
        context.Request.Path = "/tools/GetInventory";

        await pipeline.TransformAsync(context);

        context.Request.Headers["X-Ithil-TraceId"].ToString().Should().Be(traceId);
    }
}
