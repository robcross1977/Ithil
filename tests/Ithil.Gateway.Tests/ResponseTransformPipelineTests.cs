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

    private ResponseTransformPipeline CreatePipeline() =>
        new(_privacyFilter, _budgetEngine, _traceNotifier);

    [Fact]
    public async Task CallsScrubber_OnResponseBody()
    {
        var body = new MemoryStream("response body"u8.ToArray());
        _privacyFilter.ScrubAsync(Arg.Any<Stream>()).Returns("response body");

        var pipeline = CreatePipeline();
        await pipeline.TransformAsync("agent-1", "trace-abc-123", "GetInventory", body);

        await _privacyFilter.Received(1).ScrubAsync(Arg.Any<Stream>());
    }

    [Fact]
    public async Task RecordsUsage_AfterScrub()
    {
        var body = new MemoryStream("response body"u8.ToArray());
        _privacyFilter.ScrubAsync(Arg.Any<Stream>()).Returns("response body");

        var pipeline = CreatePipeline();
        await pipeline.TransformAsync("agent-1", "trace-abc-123", "GetInventory", body);

        await _budgetEngine.Received(1).RecordUsageAsync("agent-1", Arg.Any<int>());
    }

    [Fact]
    public async Task FiresTraceEvent_WithCorrectIds()
    {
        var body = new MemoryStream("response body"u8.ToArray());
        _privacyFilter.ScrubAsync(Arg.Any<Stream>()).Returns("response body");

        var pipeline = CreatePipeline();
        await pipeline.TransformAsync("agent-1", "trace-abc-123", "GetInventory", body);

        await _traceNotifier.Received(1).NotifyAsync(
            Arg.Is<AgentTraceEvent>(e =>
              e.AgentId == "agent-1" &&
              e.TraceId == "trace-abc-123"));
    }
}
