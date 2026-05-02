using FluentAssertions;
using Ithil.Core;
using Ithil.Core.Models;
using Ithil.Dashboard.Services;
using Microsoft.Extensions.Options;

namespace Ithil.Dashboard.Tests;

public class TraceFeedServiceTests
{
    private static TraceFeedService CreateService(int bufferSize = 10) =>
        new(Options.Create(new TraceOptions { BufferSize = bufferSize }));

    private static AgentTraceEvent MakeEvent(string agentId, string traceId = "t") => new()
    {
        TraceId = traceId,
        AgentId = agentId,
        ToolName = "GetStock",
        Status = "success",
        Timestamp = DateTime.UtcNow.ToString("O")
    };

    [Fact]
    public void TraceFeedService_FiltersEventsByAgentId()
    {
        var service = CreateService();
        service.Add(MakeEvent("agent-1", "t1"));
        service.Add(MakeEvent("agent-1", "t2"));
        service.Add(MakeEvent("agent-1", "t3"));
        service.Add(MakeEvent("agent-2", "t4"));
        service.Add(MakeEvent("agent-2", "t5"));

        var result = service.GetIdentified("agent-1");

        result.Should().HaveCount(3);
        result.Should().OnlyContain(e => e.AgentId == "agent-1");
    }

    [Fact]
    public void TraceFeedService_IdentifiesUnidentifiedTraffic()
    {
        var service = CreateService();
        service.Add(MakeEvent(string.Empty, "t1"));
        service.Add(MakeEvent("agent-1", "t2"));

        service.GetUnidentified().Should().HaveCount(1);
        service.GetIdentified().Should().HaveCount(1);
        service.GetIdentified().Single().AgentId.Should().Be("agent-1");
    }

    [Fact]
    public void TraceFeedService_CapsBufferAtConfiguredSize()
    {
        var service = CreateService(bufferSize: 3);
        service.Add(MakeEvent("agent-1", "oldest"));
        service.Add(MakeEvent("agent-1", "t2"));
        service.Add(MakeEvent("agent-1", "t3"));
        service.Add(MakeEvent("agent-1", "newest"));

        var all = service.GetIdentified();
        all.Should().HaveCount(3);
        all.Should().NotContain(e => e.TraceId == "oldest");
        all.Should().Contain(e => e.TraceId == "newest");
    }
}
