using System.Text.Json;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Microsoft.Extensions.Options;

namespace Ithil.Management.Audit.Sinks;

public class FileAuditSink(IOptions<FileAuditSinkOptions> options) : IAuditSink
{
    private readonly FileAuditSinkOptions _options = options.Value;

    public async Task WriteAsync(AuditRecord record)
    {
        var line = JsonSerializer.Serialize(record);
        await File.AppendAllTextAsync(_options.FilePath, line + Environment.NewLine);
    }
}
