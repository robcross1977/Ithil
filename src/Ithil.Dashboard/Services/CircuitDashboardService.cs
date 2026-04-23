using Ithil.Core.Models;
using Ithil.Dashboard.Models;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Ithil.Dashboard.Services;

/// <summary>
/// Maps Polly circuit breaker state changes to view models for the circuit breaker page.
/// </summary>
public class CircuitDashboardService
{
    private readonly Dictionary<string, CircuitBreakerViewModel> _states = [];

    /// <summary>
    /// Updates circuit state for the given agent and tool. No-ops for non-circuit events.
    /// </summary>
    public void UpdateState(AgentTraceEvent traceEvent)
    {
        if (traceEvent.CircuitState is null) return;

        MapState(traceEvent.CircuitState).IfSome(state =>
        {
            var key = $"{traceEvent.AgentId}:{traceEvent.ToolName}";
            _states[key] = new CircuitBreakerViewModel
            {
                AgentId = traceEvent.AgentId,
                ToolName = traceEvent.ToolName,
                State = state,
                LastChanged = traceEvent.Timestamp
            };
        });
    }

    /// <summary>
    /// Returns the current state of all monitored clusters.
    /// </summary>
    public Seq<CircuitBreakerViewModel> GetAll() =>
        _states.Values.ToSeq();

    private static Option<CircuitState> MapState(string circuitState) => circuitState switch
    {
        "closed" => Some(CircuitState.Closed),
        "open" => Some(CircuitState.Open),
        "half-open" => Some(CircuitState.HalfOpen),
        _ => None
    };
}
