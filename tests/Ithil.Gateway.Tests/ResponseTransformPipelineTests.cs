using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Transforms;
using NSubstitute;

namespace Ithil.Gateway.Tests;

public class ResponseTransformPipelineTests
{
    private readonly IPrivacyFilter _privacyFilter = Substitute.For<IPrivacyFilter>();
    private readonly IBudgetEngine _budgetEngine = Substitute.For<IBudgetEngine>();
    private readonly ITraceNotifier _traceNotifier = Substitute.For<ITraceNotifier>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ITokenCounter _tokenCounter = Substitute.For<ITokenCounter>();

    private ResponseTransformPipeline CreatePipeline() =>
        new(_privacyFilter, _budgetEngine, _traceNotifier, _auditLogger, _tokenCounter);

    [Fact]
    public async Task CallsScrubber_OnResponseBody()
    {
        MemoryStream body = new("response body"u8.ToArray());
        _privacyFilter.ScrubAsync(Arg.Any<Stream>()).Returns("response body");

        var pipeline = CreatePipeline();
        await pipeline.TransformAsync("agent-1", "trace-abc-123", "GetInventory", body, null);

        await _privacyFilter.Received(1).ScrubAsync(Arg.Any<Stream>());
    }

    [Fact]
    public async Task RecordsUsage_AfterScrub()
    {
        MemoryStream body = new("response body"u8.ToArray());
        _privacyFilter.ScrubAsync(Arg.Any<Stream>()).Returns("response body");

        var pipeline = CreatePipeline();
        await pipeline.TransformAsync("agent-1", "trace-abc-123", "GetInventory", body, null);

        await _budgetEngine.Received(1).RecordUsageAsync("agent-1", Arg.Any<int>());
    }

    [Fact]
    public async Task ResponseTransformPipeline_UsesTokenCounter_NotWordSplit()
    {
        MemoryStream body = new("this has four words"u8.ToArray());
        _privacyFilter.ScrubAsync(Arg.Any<Stream>()).Returns("this has four words");
        _tokenCounter.CountTokens(Arg.Any<string>()).Returns(99);

        var pipeline = CreatePipeline();
        await pipeline.TransformAsync("agent-1", "trace-abc-123", "GetInventory", body, null);

        await _budgetEngine.Received(1).RecordUsageAsync("agent-1", 99);
    }

    [Fact]
    public async Task ResponseTransformPipeline_PassesScrubbedBody_ToTokenCounter()
    {
        MemoryStream body = new("raw body"u8.ToArray());
        _privacyFilter.ScrubAsync(Arg.Any<Stream>()).Returns("scrubbed body");

        var pipeline = CreatePipeline();
        await pipeline.TransformAsync("agent-1", "trace-abc-123", "GetInventory", body, null);

        _tokenCounter.Received(1).CountTokens("scrubbed body");
    }

    [Fact]
    public async Task FiresTraceEvent_WithCorrectIds()
    {
        MemoryStream body = new("response body"u8.ToArray());
        _privacyFilter.ScrubAsync(Arg.Any<Stream>()).Returns("response body");

        var pipeline = CreatePipeline();
        await pipeline.TransformAsync("agent-1", "trace-abc-123", "GetInventory", body, null);

        await _traceNotifier
            .Received(1)
            .NotifyAsync(
                Arg.Is<AgentTraceEvent>(e => e.AgentId == "agent-1" && e.TraceId == "trace-abc-123")
            );
    }
}
