using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Core.Interfaces;

/// <summary>
/// A semantic cache that matches tool calls by meaning rather than exact parameter equality.
/// </summary>
public interface ISemanticCache
{
    /// <summary>
    /// Returns a cached result if a semantically similar call exists above the similarity threshold.
    /// Returns None on a miss, or if Redis or the embedding service are unavailable (fail-open).
    /// </summary>
    Task<Option<CacheResult>> TryGetAsync(string toolName, object parameters);

    /// <summary>
    /// Stores the response for a tool call so future similar calls can retrieve it.
    /// </summary>
    Task SetAsync(string toolName, object parameters, object response, TimeSpan ttl);
}
