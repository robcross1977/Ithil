using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Core.Interfaces;

/// <summary>
/// Holds a short-term, in-memory record of recent trace events.
/// Gives the dashboard something to display when it connects without waiting for new events.
/// </summary>
public interface ITraceBuffer
{
    /// <summary>
    /// Adds an event to the buffer. When the buffer is full, the oldest event is overwritten.
    /// </summary>
    Unit Add(AgentTraceEvent traceEvent);

    /// <summary>
    /// Returns up to <paramref name="count"/> recent events, newest first.
    /// Returns fewer than <paramref name="count"/> items without error when the buffer is not full.
    /// </summary>
    Seq<AgentTraceEvent> GetRecent(int count);
}
