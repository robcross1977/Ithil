using System.Text.Json;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;

namespace Ithil.Management.Audit.Sinks;

public class StdoutAuditSink : IAuditSink
{
    public Task WriteAsync(AuditRecord record)
    {
        var line = JsonSerializer.Serialize(record);
        Console.WriteLine(line);
        return Task.CompletedTask;
    }
}
