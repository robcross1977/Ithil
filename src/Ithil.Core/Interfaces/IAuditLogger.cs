using Ithil.Core.Models;

namespace Ithil.Core.Interfaces;

/// <summary>
/// Writes audit records for completed tool calls.
/// Implementations fan the record out to one or more <see cref="IAuditSink"/> destinations.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Writes an audit record. Called once per completed request after the response has been sent.
    /// </summary>
    Task WriteAsync(AuditRecord record, CancellationToken cancellationToken = default);
}

