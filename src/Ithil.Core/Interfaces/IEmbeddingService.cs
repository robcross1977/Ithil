namespace Ithil.Core.Interfaces;

/// <summary>
/// Converts a text string into a vector embedding for semantic similarity comparison.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// True once the underlying model has finished loading and is ready to embed.
    /// Consumed by the readiness health check so Kubernetes does not route traffic
    /// to a pod whose model failed to load.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    ///  Generates a float vector representing the semantic meaning of the input text.
    ///  The vector dimension depends on the underlying model (384 for all-MiniLM-L6-V2).
    /// </summary>
    Task<float[]> EmbedAsync(string text);
}
