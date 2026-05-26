using Ithil.Core.Models;

namespace Ithil.Core.Interfaces;

/// <summary>
/// A single destination for audit records (e.g. Redis stream, in-memory store, structured log).
/// Registered sinks are called by <see cref="IAuditLogger"/> for every completed request.
/// </summary>
public interface IAuditSink
{
    /// <summary>
    /// Persists or forwards one audit record to this sink's backing store.
    /// </summary>
    Task WriteAsync(AuditRecord record);
}

