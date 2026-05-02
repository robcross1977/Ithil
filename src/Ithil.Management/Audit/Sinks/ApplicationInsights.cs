using Ithil.Core.Interfaces;
using Ithil.Core.Models;

namespace Ithil.Management.Audit.Sinks;

public class ApplicationInsights : IAuditSink
{
    public Task WriteAsync(AuditRecord record) => throw new NotImplementedException();
}

