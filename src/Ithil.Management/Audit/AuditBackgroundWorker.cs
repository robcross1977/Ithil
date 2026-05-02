using System.Threading.Channels;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Microsoft.Extensions.Hosting;

namespace Ithil.Management.Audit;

public class AuditBackgroundWorker(Channel<AuditRecord> channel, IEnumerable<IAuditSink> sinks)
    : BackgroundService
{
    private readonly Channel<AuditRecord> _channel = channel;
    private readonly IEnumerable<IAuditSink> _sinks = sinks;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var record in _channel.Reader.ReadAllAsync(stoppingToken))
                await ProcessRecordAsync(record);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown signalled. Drain any records already queued so the audit
            // trail isn't lost just because the process is exiting. Bounded in
            // time by HostOptions.ShutdownTimeout (host force-exits after that).
            while (_channel.Reader.TryRead(out var record))
                await ProcessRecordAsync(record);
        }
    }

    private async Task ProcessRecordAsync(AuditRecord record)
    {
        var sinks = _sinks.ToList();
        if (sinks.Count == 0)
        {
            await Console.Error.WriteLineAsync(
                $"[AuditBackgroundWorker] No sinks registered — record lost: {record.TraceId}");
            return;
        }

        foreach (var sink in sinks)
        {
            try
            {
                await sink.WriteAsync(record);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(
                    $"[AuditBackgroundWorker] Sink {sink.GetType().Name} failed: {ex.Message}");
            }
        }
    }
}
