using System.Net;
using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Gateway.Resilience;
using NSubstitute;
using Polly;
using Polly.CircuitBreaker;

namespace Ithil.Gateway.Tests.Resilience;

public class CircuitBreakerPolicyFactoryTests
{
    // Short brerak duration so half-open tests don't need to wait 30 seconds.
    private readonly CircuitBreakerOptions _options = new()
    {
        MinimumThroughput = 5,
        FailureRatio = 1.0,
        SamplingDuration = TimeSpan.FromSeconds(10),
        BreakDuration = TimeSpan.FromMilliseconds(500),
    };

    private readonly ITraceNotifier _notifier = Substitute.For<ITraceNotifier>();

    private ResiliencePipeline<HttpResponseMessage> BuildPipeline() =>
        CircuitBreakerPolicyFactory.Create(_options, _notifier, "agent-1", "GetStock");

    private static ValueTask<HttpResponseMessage> Success() =>
        ValueTask.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    private static ValueTask<HttpResponseMessage> Failure() =>
        new(Task.FromException<HttpResponseMessage>(new HttpRequestException("downstream down")));

    private static async Task TriggerFailures(
        ResiliencePipeline<HttpResponseMessage> pipeline,
        int count
    )
    {
        for (var i = 0; i < count; i++)
        {
            var act = async () => await pipeline.ExecuteAsync(_ => Failure());
            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }

    [Fact]
    public async Task CircuitBreaker_RemainsOpen_After5Failures()
    {
        var pipeline = BuildPipeline();
        await TriggerFailures(pipeline, 5);

        var act = async () => await pipeline.ExecuteAsync(_ => Success());
        await act.Should().ThrowAsync<BrokenCircuitException>();
    }

    [Fact]
    public async Task CircuitBreaker_Closes_AfterSuccessfulProbe()
    {
        var pipeline = BuildPipeline();
        await TriggerFailures(pipeline, 5);

        // Wait past the break duration so the circuit transitions to half-open
        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Probe succeeds - circuit should close
        var result = await pipeline.ExecuteAsync(_ => Success(), TestContext.Current.CancellationToken);
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        // Next call hits downstream normally (not BrokenCircuitException)
        var result2 = await pipeline.ExecuteAsync(_ => Success(), TestContext.Current.CancellationToken);
        result2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CircuitBreaker_Reopens_AfterFailedProbe()
    {
        var pipeline = BuildPipeline();
        await TriggerFailures(pipeline, 5);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        var probeFail = async () => await pipeline.ExecuteAsync(_ => Failure());
        await probeFail.Should().ThrowAsync<HttpRequestException>();

        var act = async () => await pipeline.ExecuteAsync(_ => Success());
        await act.Should().ThrowAsync<BrokenCircuitException>();
    }

    [Fact]
    public async Task CircuitBreaker_FiresOpenEvent_WhenCircuitOpens()
    {
        var pipeline = BuildPipeline();
        await TriggerFailures(pipeline, 5);

        await _notifier
            .Received(1)
            .NotifyAsync(Arg.Is<AgentTraceEvent>(e => e.CircuitState == "open"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CircuitBreaker_FiresClosedEvent_WhenCirrcuitCloses()
    {
        var pipeline = BuildPipeline();
        await TriggerFailures(pipeline, 5);

        await Task.Delay(500, TestContext.Current.CancellationToken);
        await pipeline.ExecuteAsync(_ => Success(), TestContext.Current.CancellationToken);

        await _notifier
            .Received(1)
            .NotifyAsync(Arg.Is<AgentTraceEvent>(e => e.CircuitState == "closed"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CircuitBreaker_FiresHalfOpenEvent_WhenCircuitEntersHalfOpen()
    {
        var pipeline = BuildPipeline();
        await TriggerFailures(pipeline, 5);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Trigger the probe (half-open → one request allowed through)
        var act = async () => await pipeline.ExecuteAsync(_ => Failure());
        await act.Should().ThrowAsync<HttpRequestException>();

        await _notifier
            .Received(1)
            .NotifyAsync(Arg.Is<AgentTraceEvent>(e =>
                e.CircuitState == "half-open" && e.Status == "circuit-half-open"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CircuitBreaker_HalfOpenEvent_IncludesAgentIdAndToolName()
    {
        var pipeline = BuildPipeline();
        await TriggerFailures(pipeline, 5);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        var act = async () => await pipeline.ExecuteAsync(_ => Failure(), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<HttpRequestException>();

        await _notifier
            .Received(1)
            .NotifyAsync(Arg.Is<AgentTraceEvent>(e =>
                e.CircuitState == "half-open" &&
                e.AgentId == "agent-1" &&
                e.ToolName == "GetStock"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CircuitBrerakerOpotins_DefaultValues()
    {
        CircuitBreakerOptions defaults = new();

        defaults.MinimumThroughput.Should().Be(5);
        defaults.FailureRatio.Should().Be(1.0);
        defaults.BreakDuration.Should().Be(TimeSpan.FromSeconds(30));
    }
}
