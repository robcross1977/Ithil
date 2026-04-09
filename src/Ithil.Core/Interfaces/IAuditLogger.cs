using Ithil.Core.Models;

namespace Ithil.Core.Interfaces;

public interface IAuditLogger
{
    Task WriteAsync(AuditRecord record, CancellationToken cancellationToken = default);
}

