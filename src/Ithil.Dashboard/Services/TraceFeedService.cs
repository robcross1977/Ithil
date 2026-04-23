using Ithil.Core;
using Ithil.Core.Models;
using LanguageExt;
using Microsoft.Extensions.Options;

namespace Ithil.Dashboard.Services;

/// <summary>
/// Filters and aggregates trace events for the live trace feed page.
/// Maintains a capped buffer of recent events, separating identified
/// from unidentified traffic.
/// </summary>
public class TraceFeedService(IOptions<TraceOptions> options)
{
    private readonly List<AgentTraceEvent> _buffer = [];
    private readonly int _bufferSize = options.Value.BufferSize;

    /// <summary>
    /// Adds an event to the front of the buffer. When the buffer
    /// exceeds the configured size, the oldest event is dropped.
    /// </summary>
    public void Add(AgentTraceEvent traceEvent)
    {
        _buffer.Insert(0, traceEvent);
        if (_buffer.Count > _bufferSize)
            _buffer.RemoveAt(_buffer.Count - 1);
    }

    /// <summary>
    /// Returns identified events, newest first. Optionally filters to a single agent.
    /// </summary>
    public Seq<AgentTraceEvent> GetIdentified(string? agentIdFilter = null) =>
        _buffer
            .Where(e => !string.IsNullOrWhiteSpace(e.AgentId) &&
                        (agentIdFilter is null || e.AgentId == agentIdFilter))
            .ToSeq();

    /// <summary>
    /// Returns events with no resolved agent identity, newest first.
    /// </summary>
    public Seq<AgentTraceEvent> GetUnidentified() =>
        _buffer
            .Where(e => string.IsNullOrWhiteSpace(e.AgentId))
            .ToSeq();
}
