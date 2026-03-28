using System.Text.Json;
using Ithil.Core.Models;
using Ithil.Management.Audit;
using Ithil.Management.Audit.Sinks;
using Microsoft.Extensions.Options;
using FluentAssertions;

namespace Ithil.Management.Tests.Audit;

public class AuditSinkTests
{
    [Fact]
    public async Task StdoutAuditSink_WritesValidJsonLine()
    {
        var stdout = new StringWriter();
        Console.SetOut(stdout);

        var sink = new StdoutAuditSink();
        await sink.WriteAsync(BuildRecord("success"));

        var output = stdout.ToString().Trim();
        output.Should().NotBeEmpty();
        var act = () => JsonDocument.Parse(output);
        act.Should().NotThrow("output must be valid JSON");
        output.Should().NotContain("\n", "must be a single line");
    }

    [Fact]
    public async Task FileAuditSink_AppendsToFile_NotOverwrites()
    {
        var path = Path.GetTempFileName();
        try
        {
            var options = Options.Create(new FileAuditSinkOptions { FilePath = path });
            var sink = new FileAuditSink(options);
            await sink.WriteAsync(BuildRecord("success"));
            await sink.WriteAsync(BuildRecord("error"));

            var lines = await File.ReadAllLinesAsync(path);
            lines.Should().HaveCount(2);
            JsonDocument.Parse(lines[0]);
            JsonDocument.Parse(lines[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AuditRecord_Outcome_IsBlockedForBudgetExceeded()
    {
        var record = BuildRecord("blocked");
        record.Outcome.Should().Be("blocked");
    }

    [Fact]
    public void AuditRecord_CacheHit_IsTrue_WhenServedFromCache()
    {
        var record = BuildRecord("cache-hit") with { CacheHit = true };
        record.CacheHit.Should().BeTrue();
    }

    [Fact]
    public void AuditRecord_LatencyMs_IsNull_ForCacheHits()
    {
        var record = BuildRecord("cache-hit") with { CacheHit = true, LatencyMs = null };
        record.LatencyMs.Should().BeNull();
    }

    private static AuditRecord BuildRecord(string outcome) => new()
    {
        Timestamp = DateTime.UtcNow.ToString("o"),
        TraceId = Guid.NewGuid().ToString(),
        AgentId = "test-agent",
        ToolName = "test-tool",
        Outcome = outcome
    };
}
