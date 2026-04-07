using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Transforms;
using NSubstitute;

namespace Ithil.Gateway.Tests;

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

    [Fact]
    public async Task ExecuteAsync_ReturnsScrubbed_WhenBudgetOk()
    {
        _budgetEngine.IsWithinBudgetAsync("agent-1", Arg.Any<CancellationToken>()).Returns(true);
        _traceIdFactory.Create().Returns("trace-1");
        _privacyFilter.ScrubAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns("scrubbed");
        _tokenCounter.CountTokens("scrubbed").Returns(5);

        var result = await CreatePipeline().ExecuteAsync(
            "agent-1", "GetInventory", () => Task.FromResult("raw"), CancellationToken.None);

        result.Should().Be("scrubbed");
    }

    [Fact]
    public async Task ExecuteAsync_EmitsSuccessTraceAndAudit_WhenCallSucceeds()
    {
        _budgetEngine.IsWithinBudgetAsync("agent-1", Arg.Any<CancellationToken>()).Returns(true);
        _traceIdFactory.Create().Returns("trace-1");
        _privacyFilter.ScrubAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns("scrubbed");
        _tokenCounter.CountTokens(Arg.Any<string>()).Returns(5);

        await CreatePipeline().ExecuteAsync(
            "agent-1", "GetInventory", () => Task.FromResult("raw"), CancellationToken.None);

        await _traceNotifier.Received().NotifyAsync(
            Arg.Is<AgentTraceEvent>(e => e.Status == "success"), Arg.Any<CancellationToken>());
        await _auditLogger.Received().WriteAsync(
            Arg.Is<AuditRecord>(r => r.Outcome == "success"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsAndEmitsDenied_WhenBudgetExceeded()
    {
        _budgetEngine.IsWithinBudgetAsync("agent-1", Arg.Any<CancellationToken>()).Returns(false);
        _traceIdFactory.Create().Returns("trace-denied");

        var act = () => CreatePipeline().ExecuteAsync(
            "agent-1", "GetInventory", () => Task.FromResult("raw"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _traceNotifier.Received().NotifyAsync(
            Arg.Is<AgentTraceEvent>(e => e.Status == "denied"), Arg.Any<CancellationToken>());
        await _auditLogger.Received().WriteAsync(
            Arg.Is<AuditRecord>(r => r.Outcome == "denied"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EmitsErrorTraceAndAudit_WhenDownstreamThrows()
    {
        _budgetEngine.IsWithinBudgetAsync("agent-1", Arg.Any<CancellationToken>()).Returns(true);
        _traceIdFactory.Create().Returns("trace-err");

        var act = () => CreatePipeline().ExecuteAsync(
            "agent-1", "GetInventory",
            () => throw new HttpRequestException("downstream failure"),
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        await _traceNotifier.Received().NotifyAsync(
            Arg.Is<AgentTraceEvent>(e => e.Status == "error"), Arg.Any<CancellationToken>());
        await _auditLogger.Received().WriteAsync(
            Arg.Is<AuditRecord>(r => r.Outcome == "error"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EmitsCancelledStatus_WhenOperationCancelled()
    {
        _budgetEngine.IsWithinBudgetAsync("agent-1", Arg.Any<CancellationToken>()).Returns(true);
        _traceIdFactory.Create().Returns("trace-cancel");

        var act = () => CreatePipeline().ExecuteAsync(
            "agent-1", "GetInventory",
            () => throw new OperationCanceledException(),
            CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _traceNotifier.Received().NotifyAsync(
            Arg.Is<AgentTraceEvent>(e => e.Status == "cancelled"), Arg.Any<CancellationToken>());
        await _auditLogger.Received().WriteAsync(
            Arg.Is<AuditRecord>(r => r.Outcome == "cancelled"), Arg.Any<CancellationToken>());
    }
}
