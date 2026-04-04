using System.Threading.Channels;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;

namespace Ithil.Management.Audit;

public class AuditLogger(Channel<AuditRecord> channel) : IAuditLogger
{
    private readonly Channel<AuditRecord> _channel = channel;

    public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        _channel.Writer.TryWrite(record);
        return Task.CompletedTask;
    }
}
