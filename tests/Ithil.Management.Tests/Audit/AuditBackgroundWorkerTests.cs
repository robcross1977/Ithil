using System.Threading.Channels;
using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Management.Audit;
using NSubstitute;

namespace Ithil.Management.Tests.Audit;

/// <summary>
/// Verifies the graceful-shutdown drain: records queued before cancellation
/// must not be lost, because HostOptions.ShutdownTimeout bounds how long the
/// drain can run before the host force-exits.
/// </summary>
public class AuditBackgroundWorkerTests
{
    // Exposes the protected ExecuteAsync for deterministic testing.
    private sealed class TestWorker(Channel<AuditRecord> channel, IEnumerable<IAuditSink> sinks)
        : AuditBackgroundWorker(channel, sinks)
    {
        public Task RunAsync(CancellationToken ct) => ExecuteAsync(ct);
    }

    [Fact]
    public async Task AuditBackgroundWorker_DrainsPendingRecords_OnShutdown()
    {
        // Arrange: queue 3 records before the worker ever starts,
        // then pass a pre-cancelled token so it goes straight to the drain path.
        var channel = Channel.CreateUnbounded<AuditRecord>();
        var sink = Substitute.For<IAuditSink>();

        await channel.Writer.WriteAsync(BuildRecord("r1"));
        await channel.Writer.WriteAsync(BuildRecord("r2"));
        await channel.Writer.WriteAsync(BuildRecord("r3"));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var worker = new TestWorker(channel, [sink]);
        await worker.RunAsync(cts.Token);

        // Assert: all 3 pre-queued records were drained to the sink.
        await sink.Received(3).WriteAsync(Arg.Any<AuditRecord>());
    }

    private static AuditRecord BuildRecord(string traceId) => new()
    {
        Timestamp = DateTime.UtcNow.ToString("o"),
        TraceId = traceId,
        AgentId = "test-agent",
        ToolName = "test-tool",
        Outcome = "success",
    };
}
