using FluentAssertions;
using Ithil.Core.Models;
using Ithil.Gateway.Tracing;
using Microsoft.Extensions.Options;

namespace Ithil.Gateway.Tests.Tracing;

public class TraceRingBufferTests
{
    private static TraceRingBuffer Buffer(int size) =>
        new(Options.Create(new TraceOptions { BufferSize = size }));

    private static AgentTraceEvent Event(string id) => new()
    {
        TraceId = "t1", AgentId = id, ToolName = "tool", Status = "success", Timestamp = "2026-01-01T00:00:00Z"
    };

    [Fact]
    public void StoresAndReturns_WhenBelowCapacity()
    {
        var buf = Buffer(5);
        buf.Add(Event("A")); buf.Add(Event("B")); buf.Add(Event("C"));
        buf.GetRecent(5).Count().Should().Be(3);
    }

    [Fact]
    public void OverwritesOldest_WhenFull()
    {
        var buf = Buffer(5);
        for (var i = 1; i <= 6; i++) buf.Add(Event($"E{i}"));
        var recent = buf.GetRecent(5);
        recent.Any(e => e.AgentId == "E1").Should().BeFalse();
        recent.Any(e => e.AgentId == "E6").Should().BeTrue();
    }

    [Fact]
    public void ReturnsNewestFirst()
    {
        var buf = Buffer(5);
        buf.Add(Event("A")); buf.Add(Event("B")); buf.Add(Event("C"));
        var recent = buf.GetRecent(3).ToList();
        recent[0].AgentId.Should().Be("C");
        recent[1].AgentId.Should().Be("B");
        recent[2].AgentId.Should().Be("A");
    }

    [Fact]
    public void RespectsCountParameter()
    {
        var buf = Buffer(20);
        for (var i = 0; i < 10; i++) buf.Add(Event($"E{i}"));
        buf.GetRecent(3).Count().Should().Be(3);
    }

    [Fact]
    public void ReturnsEmpty_WhenNoEventsAdded()
    {
        Buffer(10).GetRecent(10).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsThreadSafe_UnderConcurrentWrites()
    {
        var buf = Buffer(500);
        var threads = Enumerable.Range(0, 10)
            .Select(_ => new Thread(() =>
            {
                for (var i = 0; i < 50; i++) buf.Add(Event("X"));
            }))
            .ToList();

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        var act = () => buf.GetRecent(500).ToList();
        act.Should().NotThrow();
        var recent = buf.GetRecent(500).ToList();
        recent.Should().HaveCount(500);
    }
}
