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

        var pipeline = CreatePipeline();
        DefaultHttpContext context = new();
        context.Request.Path = "/tools/GetInventory";

        await pipeline.TransformAsync(context);

        context.Request.Headers["X-Ithil-TraceId"].ToString().Should().Be(traceId);
    }
}
