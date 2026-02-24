namespace Ithil.Core.Interfaces;

/// <summary>
/// Creates unique trace IDs for request correlation.
/// </summary>
public interface ITraceIdFactory
{
    /// <summary>
    /// Generates a new unique trace ID.
    /// </summary>
    string Create();
}
