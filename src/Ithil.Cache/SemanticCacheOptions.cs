using Ithil.Core.Enums;

namespace Ithil.Cache;

/// <summary>
/// Configuration options for the Semantic Cache.
/// Loaded from appsettings.json under "Ithil:SemanticCache".
/// </summary>
public class SemanticCacheOptions
{
    /// <summary>
    /// Minimum cosing similarity score required to count as a cache hit.
    /// Range 0.0-1.0. Higher = stricter matching, fewer false hits.
    /// Default 0.95 is conservative - wrong cached data is worse than a miss.
    /// </summary>
    public float SimilarityThreshold { get; set; } = 0.95f;

    /// <summary>
    /// How long a cached entry lives in Redis before expiring.
    /// Default 15 minutes - suitable for frequently-read but occassionally-changing data.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// File path to the ONNX model used for generating embeddings.
    /// Default points to the models/ folder at the solution root.
    /// </summary>
    public string ModelPath { get; set; } = "models/all-MiniLM-L6-v2.onnx";

    /// <summary>
    /// File path to the vocabulary file used be the BERT tokenizer.
    /// Must match the tokenizer the ONNX model was trained with.
    /// </summary>
    public string VocabPath { get; set; } = "models/vocab.txt";

    /// <summary>
    /// How the cache behaves when Redis is unavailable.
    /// FailOpen treats all errors as cache misses; FailClosed propagates the failure
    /// so the pipeline can return 503. Write failures are non-fatal under both policies.
    /// Defaults to FailOpen to preserve existing behaviour.
    /// </summary>
    public RedisFailurePolicy FailurePolicy { get; set; } = RedisFailurePolicy.FailOpen;
}
