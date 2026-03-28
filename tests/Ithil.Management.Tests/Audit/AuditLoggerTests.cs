using System.Threading.Channels;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Management.Audit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using FluentAssertions;

namespace Ithil.Management.Tests.Audit;

public class AuditLoggerTests
{
    // AuditLogger takes the shared Channel<AuditRecord> and writes to its Writer side.
    // AuditBackgroundWorker takes the same channel and drains from its Reader side.
    // Both get the same singleton channel injected by DI — no hidden coordination.

    [Fact]
    public async Task AuditLogger_Enqueues_DoesNotBlockCaller()
    {
        var channel = Channel.CreateUnbounded<AuditRecord>();
        var logger = new AuditLogger(channel);
        var record = BuildRecord("success");

        var start = DateTime.UtcNow;
        await logger.WriteAsync(record);
        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;

        elapsed.Should().BeLessThan(100);
        channel.Reader.TryRead(out var queued).Should().BeTrue();
        queued.Should().Be(record);
    }

    [Fact]
    public async Task AuditBackgroundWorker_CallsAllSinks_ForEachRecord()
    {
        var channel = Channel.CreateUnbounded<AuditRecord>();
        var sink1 = Substitute.For<IAuditSink>();
        var sink2 = Substitute.For<IAuditSink>();
        var worker = new AuditBackgroundWorker(channel, [sink1, sink2]);

        var cts = new CancellationTokenSource();
        _ = worker.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(BuildRecord("success"));
        await channel.Writer.WriteAsync(BuildRecord("success"));
        await channel.Writer.WriteAsync(BuildRecord("success"));

        await Task.Delay(200);
        await cts.CancelAsync();

        await sink1.Received(3).WriteAsync(Arg.Any<AuditRecord>());
        await sink2.Received(3).WriteAsync(Arg.Any<AuditRecord>());
    }

    [Fact]
    public async Task AuditBackgroundWorker_WritesToStderr_WhenAllSinksFail()
    {
        var channel = Channel.CreateUnbounded<AuditRecord>();
        var sink1 = Substitute.For<IAuditSink>();
        var sink2 = Substitute.For<IAuditSink>();
        sink1.WriteAsync(Arg.Any<AuditRecord>()).ThrowsAsync(new Exception("sink1 dead"));
        sink2.WriteAsync(Arg.Any<AuditRecord>()).ThrowsAsync(new Exception("sink2 dead"));

        var worker = new AuditBackgroundWorker(channel, [sink1, sink2]);

        var stderr = new StringWriter();
        Console.SetError(stderr);

        var cts = new CancellationTokenSource();
        _ = worker.StartAsync(cts.Token);

        await channel.Writer.WriteAsync(BuildRecord("success"));
        await Task.Delay(200);
        await cts.CancelAsync();

        stderr.ToString().Should().NotBeEmpty();
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
