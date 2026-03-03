namespace Ithil.Core.Interfaces;

/// <summary>
/// Converts a text string into a vector embedding for semantic similarity comparison.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    ///  Generates a float vector representing the semantic meaning of the input text.
    ///  The vector dimension depends on the underlying model (384 for all-MiniLM-L6-V2).
    /// </summary>
    Task<float[]> EmbedAsync(string text);
}
