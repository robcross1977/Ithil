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

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await foreach (var record in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            var sinks = _sinks.ToList();

            if (sinks.Count == 0)
            {
                await Console.Error.WriteLineAsync(
                    $"[AuditBackgroundWorker] No sinks registered — record lost: {record.TraceId}"
                );
                continue;
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
                        $"[AuditBackgroundWorker] Sink {sink.GetType().Name} failed: {ex.Message}"
                    );
                }
            }
        }
    }
}
