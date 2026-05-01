using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using System.Collections.Concurrent;

namespace Ithil.Integration.Tests.Fixtures;

/// <summary>
/// In-memory audit sink that collects records written during a test run.
/// Used to verify that the gateway pipeline writes correct audit data for blocked requests.
/// </summary>
public sealed class CapturingAuditSink : IAuditSink
{
    private readonly ConcurrentBag<AuditRecord> _records = new();

    /// <summary>Live collection of all records written so far.</summary>
    public IReadOnlyCollection<AuditRecord> Records => _records;

    /// <inheritdoc />
    public Task WriteAsync(AuditRecord record)
    {
        _records.Add(record);
        return Task.CompletedTask;
    }
}
