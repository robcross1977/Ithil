using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using Microsoft.Extensions.Options;

namespace Ithil.Gateway.Tracing;

/// <summary>
/// Fixed-size circular buffer that holds the most recent trace events in memory.
/// When full, the oldest event is silently overwritten — no exceptions, no resizing.
/// Thread-safe under concurrent writes.
/// </summary>
public class TraceRingBuffer(IOptions<TraceOptions> options) : ITraceBuffer
{
    // Fixed array allocated once at startup. Size never changes, so there is no GC pressure
    // during normal operation — every write is an array slot assignment plus two int increments.
    private readonly AgentTraceEvent?[] _slots = new AgentTraceEvent?[options.Value.BufferSize];

    // Index of the next slot to write into. Advances on every Add and wraps back to 0 at capacity.
    private int _writeIndex = 0;

    // Number of slots that have been filled. Caps at capacity once the buffer is full.
    private int _count = 0;

    private readonly object _lock = new();

    /// <inheritdoc />
    public Unit Add(AgentTraceEvent traceEvent)
    {
        lock (_lock)
        {
            _slots[_writeIndex] = traceEvent;

            // Advance the write pointer, wrapping around to 0 when it reaches the end.
            _writeIndex = (_writeIndex + 1) % _slots.Length;

            // Only increment count until we reach capacity — after that every write overwrites.
            if (_count < _slots.Length)
                _count++;
        }

        return Unit.Default;
    }

    /// <inheritdoc />
    public Seq<AgentTraceEvent> GetRecent(int count)
    {
        if (count <= 0)
            return Seq<AgentTraceEvent>.Empty;

        lock (_lock)
        {
            var take = Math.Min(count, _count);
            var result = new AgentTraceEvent[take];

            for (var i = 0; i < take; i++)
            {
                // Walk backwards from the most recently written slot.
                // _writeIndex points to the NEXT slot to write, so offset by -1 to get the newest.
                // Adding _slots.Length before the mod guarantees a positive result because
                // (_writeIndex - 1 - i) is always in [-_slots.Length, _slots.Length-2].
                var idx = (_writeIndex - 1 - i + _slots.Length) % _slots.Length;
                result[i] = _slots[idx]!;
            }

            return result.ToSeq();
        }
    }
}
