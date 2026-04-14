namespace Ithil.Gateway.Tracing;

/// <summary>
/// Configuration for the trace ring buffer.
/// Bind from the <c>Ithil:Trace</c> configuration section.
/// </summary>
public class TraceOptions
{
    /// <summary>
    /// Maximum number of trace events held in memory. When full, the oldest event is
    /// silently overwritten. Cleared on process restart by design.
    /// Default: 500.
    /// </summary>
    public int BufferSize { get; set; } = 500;
}
