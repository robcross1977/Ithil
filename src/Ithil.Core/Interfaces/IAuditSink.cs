using Ithil.Core.Models;

namespace Ithil.Core.Interfaces;

public interface IAuditSink
{
    Task WriteAsync(AuditRecord record);
}

