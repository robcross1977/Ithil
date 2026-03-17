using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Polly;
using Polly.CircuitBreaker;

namespace Ithil.Gateway.Resilience;

/// <summary>
/// Builds a Polly resilience pipeline with a circuit breaker for a named downstream route.
/// </summary>
internal static class CircuitBreakerPolicyFactory
{
    /// <summary>
    /// Creates a circuit breaker pipeline that fires SignalR trace events on state transitions.
    /// </summary>
    /// <param name="options">Threshold and timing configuration.</param>
    /// <param name="notifier">Broadcasts state-change events to the dashboard.</param>
    /// <param name="agentId">Agent ID included in the trace events.</param>
    /// <param name="toolName">Tool name included in trace events.</param>
    /// <returns>A resilience pipeline wrapping the configured circuit breaker.</returns>
    public static ResiliencePipeline<HttpResponseMessage> Create(
        CircuitBreakerOptions options,
        ITraceNotifier notifier,
        string agentId,
        string toolName
    ) =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(CreateStrategyOptions(options, notifier, agentId, toolName))
            .Build();

    /// <summary>
    /// Builds the circuit breaker strategy options for use with AddResilienceHandler or directly.
    /// </summary>
    public static CircuitBreakerStrategyOptions<HttpResponseMessage> CreateStrategyOptions(
        CircuitBreakerOptions options,
        ITraceNotifier notifier,
        string agentId,
        string toolName
    ) =>
        new()
        {
            MinimumThroughput = options.MinimumThroughput,
            FailureRatio = options.FailureRatio,
            SamplingDuration = options.SamplingDuration,
            BreakDuration = options.BreakDuration,

            // Treat any exception as a failure
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>().Handle<Exception>(),

            OnOpened = _ =>
            {
                notifier.NotifyAsync(
                    new AgentTraceEvent
                    {
                        TraceId = Guid.NewGuid().ToString("N"),
                        AgentId = agentId,
                        ToolName = toolName,
                        Status = "circuit-open",
                        CircuitState = "open",
                        Timestamp = DateTime.UtcNow.ToString("O"),
                    }
                );

                return ValueTask.CompletedTask;
            },

            OnClosed = _ =>
            {
                notifier.NotifyAsync(
                    new AgentTraceEvent
                    {
                        TraceId = Guid.NewGuid().ToString("N"),
                        AgentId = agentId,
                        ToolName = toolName,
                        Status = "circuit-closed",
                        CircuitState = "closed",
                        Timestamp = DateTime.UtcNow.ToString("O"),
                    }
                );

                return ValueTask.CompletedTask;
            },
        };
}
