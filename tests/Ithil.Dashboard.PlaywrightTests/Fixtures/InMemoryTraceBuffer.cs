using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Dashboard.PlaywrightTests.Fixtures;

/// <summary>
/// Seeded in-memory trace buffer for Playwright tests.
/// Seed events before the page loads via <see cref="Seed"/>; they are replayed
/// newest-first by <see cref="GetRecent"/> exactly as the real ring buffer does.
/// </summary>
public sealed class InMemoryTraceBuffer : ITraceBuffer
{
    private readonly List<AgentTraceEvent> _events = [];

    /// <summary>Appends an event that will be returned by GetRecent.</summary>
    public void Seed(AgentTraceEvent traceEvent) => _events.Add(traceEvent);

    /// <summary>Removes all seeded events.</summary>
    public void Reset() => _events.Clear();

    /// <inheritdoc/>
    public Unit Add(AgentTraceEvent traceEvent)
    {
        _events.Add(traceEvent);
        return Unit.Default;
    }

    /// <inheritdoc/>
    // Return newest-first, matching the real TraceRingBuffer contract.
    public Seq<AgentTraceEvent> GetRecent(int count) =>
        _events.AsEnumerable().Reverse().Take(count).ToSeq();
}
