using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Transforms;
using NSubstitute;

namespace Ithil.Gateway.Tests.Transforms;

public class ToolCallGovernancePipelineTests
{
    private readonly IBudgetEngine _budgetEngine = Substitute.For<IBudgetEngine>();
    private readonly IPrivacyFilter _privacyFilter = Substitute.For<IPrivacyFilter>();
    private readonly ITokenCounter _tokenCounter = Substitute.For<ITokenCounter>();
    private readonly ITraceIdFactory _traceIdFactory = Substitute.For<ITraceIdFactory>();
    private readonly ITraceNotifier _traceNotifier = Substitute.For<ITraceNotifier>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();

    private ToolCallGovernancePipeline CreatePipeline() =>
        new(_budgetEngine, _privacyFilter, _tokenCounter, _traceIdFactory, _traceNotifier, _auditLogger);

    public ToolCallGovernancePipelineTests()
    {
        _traceIdFactory.Create().Returns("trace-001");
        _budgetEngine.IsWithinBudgetAsync(Arg.Any<string>()).Returns(true);
        _privacyFilter.ScrubAsync(Arg.Any<Stream>()).Returns("scrubbed response");
        _tokenCounter.CountTokens(Arg.Any<string>()).Returns(42);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsInvalidOperationException_WhenBudgetExceeded()
    {
        _budgetEngine.IsWithinBudgetAsync("agent-1").Returns(false);
        var pipeline = CreatePipeline();

        var act = () => pipeline.ExecuteAsync("agent-1", "GetInventory", () => Task.FromResult("ok"), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*agent-1*exceeded*");
    }

    [Fact]
    public async Task ExecuteAsync_EmitsDeniedTraceAndAudit_WhenBudgetExceeded()
    {
        _budgetEngine.IsWithinBudgetAsync("agent-1").Returns(false);
        var pipeline = CreatePipeline();

        var act = () => pipeline.ExecuteAsync("agent-1", "GetInventory", () => Task.FromResult("ok"), default);

        await act.Should().ThrowAsync<InvalidOperationException>();

        await _traceNotifier.Received(1).NotifyAsync(
            Arg.Is<AgentTraceEvent>(e =>
                e.AgentId == "agent-1" &&
                e.ToolName == "GetInventory" &&
                e.Status == "denied"));

        await _auditLogger.Received(1).WriteAsync(
            Arg.Is<AuditRecord>(r =>
                r.AgentId == "agent-1" &&
                r.ToolName == "GetInventory" &&
                r.Outcome == "denied"));
    }

    [Fact]
    public async Task ExecuteAsync_ScrubsResponseAndRecordsUsage_OnSuccess()
    {
        var pipeline = CreatePipeline();

        var result = await pipeline.ExecuteAsync(
            "agent-1", "GetInventory",
            () => Task.FromResult("raw response"),
            default);

        result.Should().Be("scrubbed response");
        await _privacyFilter.Received(1).ScrubAsync(Arg.Any<Stream>());
        await _budgetEngine.Received(1).RecordUsageAsync("agent-1", 42);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsSuccessTraceAndAudit_OnSuccess()
    {
        var pipeline = CreatePipeline();

        await pipeline.ExecuteAsync("agent-1", "GetInventory", () => Task.FromResult("ok"), default);

        await _traceNotifier.Received(1).NotifyAsync(
            Arg.Is<AgentTraceEvent>(e =>
                e.AgentId == "agent-1" &&
                e.ToolName == "GetInventory" &&
                e.Status == "success"));

        await _auditLogger.Received(1).WriteAsync(
            Arg.Is<AuditRecord>(r =>
                r.AgentId == "agent-1" &&
                r.ToolName == "GetInventory" &&
                r.Outcome == "success"));
    }

    [Fact]
    public async Task ExecuteAsync_EmitsErrorTraceAndAudit_WhenDownstreamFails()
    {
        var pipeline = CreatePipeline();
        Func<Task<string>> failingInvoke = () => throw new HttpRequestException("connection refused");

        var act = () => pipeline.ExecuteAsync("agent-1", "GetInventory", failingInvoke, default);

        await act.Should().ThrowAsync<HttpRequestException>();

        await _traceNotifier.Received(1).NotifyAsync(
            Arg.Is<AgentTraceEvent>(e =>
                e.AgentId == "agent-1" &&
                e.ToolName == "GetInventory" &&
                e.Status == "error"));

        await _auditLogger.Received(1).WriteAsync(
            Arg.Is<AuditRecord>(r =>
                r.AgentId == "agent-1" &&
                r.Outcome == "error" &&
                r.ErrorMessage == "connection refused"));
    }

    [Fact]
    public async Task ExecuteAsync_PreservesOriginalStackTrace_WhenDownstreamFails()
    {
        var pipeline = CreatePipeline();

        // The original exception should propagate with its type intact, not wrapped.
        Func<Task<string>> failingInvoke = () => throw new InvalidDataException("downstream broke");

        var act = () => pipeline.ExecuteAsync("agent-1", "GetInventory", failingInvoke, default);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("downstream broke");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceled_WhenTokenAlreadyCancelled()
    {
        var pipeline = CreatePipeline();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => pipeline.ExecuteAsync("agent-1", "GetInventory", () => Task.FromResult("ok"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_EmitsPendingTrace_BeforeInvoke()
    {
        var pipeline = CreatePipeline();
        var traceStatuses = new List<string>();
        _traceNotifier
            .NotifyAsync(Arg.Do<AgentTraceEvent>(e => traceStatuses.Add(e.Status)));

        await pipeline.ExecuteAsync("agent-1", "GetInventory", () => Task.FromResult("ok"), default);

        traceStatuses.Should().Contain("pending");
    }

    [Fact]
    public async Task RecordCacheHitAsync_EmitsCacheHitTraceAndAudit()
    {
        var pipeline = CreatePipeline();

        await pipeline.RecordCacheHitAsync("agent-1", "GetInventory");

        await _traceNotifier.Received(1).NotifyAsync(
            Arg.Is<AgentTraceEvent>(e =>
                e.AgentId == "agent-1" &&
                e.ToolName == "GetInventory" &&
                e.Status == "cache-hit" &&
                e.TokensUsed == 0));

        await _auditLogger.Received(1).WriteAsync(
            Arg.Is<AuditRecord>(r =>
                r.AgentId == "agent-1" &&
                r.ToolName == "GetInventory" &&
                r.Outcome == "cache-hit" &&
                r.CacheHit == true &&
                r.TokensUsed == 0));
    }

    [Fact]
    public async Task RecordCacheHitAsync_DoesNotCheckOrRecordBudget()
    {
        var pipeline = CreatePipeline();

        await pipeline.RecordCacheHitAsync("agent-1", "GetInventory");

        await _budgetEngine.DidNotReceive().IsWithinBudgetAsync(Arg.Any<string>());
        await _budgetEngine.DidNotReceive().RecordUsageAsync(Arg.Any<string>(), Arg.Any<int>());
    }
}
