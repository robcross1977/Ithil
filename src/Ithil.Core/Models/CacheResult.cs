namespace Ithil.Core.Models;

/// <summary>
/// A result returned from the semantic cache on a cache hit.
/// Contains the stored response and the similarity score that matched it.
/// </summary>
public record CacheResult
{
    /// <summary>
    /// The serialized response body stored when this entry was cached.
    /// </summary>
    public required string SerializedResponse { get; init; }

    /// <summary>
    /// Cosine similarity score between the query vector and this entry's vector.
    /// Range: 0.0 (completely different) to 1.0 (identical). Typically > 0.95 for a cache hit.
    /// </summary>
    public float Similarity { get; init; }
}

