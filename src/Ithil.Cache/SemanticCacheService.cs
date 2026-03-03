using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ithil.Cache;

/// <summary>
/// Redis-backed semantic cache. Matches tool calls by meaning rather than exact parameters.
/// Uses vector embeddings and cosine similarity to find cache hits.
/// Fails open — any error (Redis down, model unavailable) is treated as a cache miss.
/// </summary>
public class SemanticCacheService : ISemanticCache
{
    private readonly IEmbeddingService _embedder;
    private readonly IDatabase _redis;
    private readonly SemanticCacheOptions _options;

    // Index creation is idempotent but we only need to attempt it once per instance.
    private bool _indexCreated;
    private readonly SemaphoreSlim _indexLock = new(1, 1);

    // The RediSearch index name. All cache keys use the "cache:" prefix so the
    // index only covers cache entries, not other Redis keys in the database.
    private const string IndexName = "ithil-cache-idx";
    private const string KeyPrefix = "cache:";
    private const int EmbeddingDims = 384;

    public SemanticCacheService(
        IEmbeddingService embedder,
        IDatabase redis,
        SemanticCacheOptions options)
    {
        _embedder = embedder;
        _redis = redis;
        _options = options;
    }

    /// <summary>
    /// Looks up a semantically similar cached response.
    /// Returns None on a miss or any failure — callers should proceed to the downstream service.
    /// </summary>
    public async Task<Option<CacheResult>> TryGetAsync(string toolName, object parameters)
    {
        try
        {
            await EnsureIndexAsync();

            // Build a deterministic intent string and embed it.
            var intent = IntentSerializer.Serialize(toolName, parameters);
            var vector = await _embedder.EmbedAsync(intent);
            var vectorBytes = VectorToBytes(vector);

            // FT.SEARCH with KNN 1: find the single nearest neighbor to our query vector.
            // The AS score clause names the distance result — RediSearch COSINE returns
            // distance (1 - cosine_similarity), NOT the similarity itself.
            // PARAMS 2 vec <bytes>: pass the query vector as a parameter.
            // DIALECT 2: required for vector search syntax.
            var result = await _redis.ExecuteAsync("FT.SEARCH",
                IndexName,
                "*=>[KNN 1 @embedding $vec AS score]",
                "PARAMS", "2", "vec", vectorBytes,
                "RETURN", "2", "response", "score",
                "DIALECT", "2");

            return ParseSearchResult(result);
        }
        catch (Exception)
        {
            // Fail open — Redis down, model error, parse failure all become a cache miss.
            return Option<CacheResult>.None;
        }
    }

    /// <summary>
    /// Stores a tool response in the cache with the given TTL.
    /// Silently swallows failures — a failed write is not an error.
    /// </summary>
    public async Task SetAsync(string toolName, object parameters, object response, TimeSpan ttl)
    {
        try
        {
            await EnsureIndexAsync();

            var intent = IntentSerializer.Serialize(toolName, parameters);
            var vector = await _embedder.EmbedAsync(intent);
            var vectorBytes = VectorToBytes(vector);
            var serializedResponse = JsonSerializer.Serialize(response);

            // Use a hash of the intent as the key — short, unique, deterministic.
            var key = $"{KeyPrefix}{ComputeHash(intent)}";

            // HSET stores the response body and the embedding vector as a Redis hash.
            // The vector is stored as raw float32 bytes — required by RediSearch VECTOR.
            await _redis.ExecuteAsync("HSET",
                key,
                "response", serializedResponse,
                "embedding", vectorBytes);

            // Set expiry so stale entries don't accumulate indefinitely.
            await _redis.ExecuteAsync("EXPIRE", key, (long)ttl.TotalSeconds);
        }
        catch (Exception)
        {
            // Swallow — cache write failures are non-fatal.
        }
    }

    // Creates the RediSearch vector index the first time it's needed.
    // Uses double-checked locking to avoid redundant creation across concurrent requests.
    private async Task EnsureIndexAsync()
    {
        if (_indexCreated) return;
        await _indexLock.WaitAsync();
        try
        {
            if (_indexCreated) return;
            try
            {
                // FT.CREATE defines the index schema:
                // - ON HASH: index Redis hashes (not JSON or other types)
                // - PREFIX 1 cache:: only index keys starting with "cache:"
                // - response TEXT: full-text searchable response body
                // - embedding VECTOR FLAT 6 ...: flat (brute-force) vector index
                //   FLAT is simpler than HNSW and accurate for small datasets (<100k entries)
                //   TYPE FLOAT32: each dimension stored as a 4-byte float
                //   DIM 384: matches all-MiniLM-L6-v2 output dimension
                //   DISTANCE_METRIC COSINE: measures angle between vectors
                await _redis.ExecuteAsync("FT.CREATE",
                    IndexName,
                    "ON", "HASH",
                    "PREFIX", "1", KeyPrefix,
                    "SCHEMA",
                    "response", "TEXT",
                    "score",    "NUMERIC",
                    "embedding", "VECTOR", "FLAT",
                    "6",
                    "TYPE",            "FLOAT32",
                    "DIM",             EmbeddingDims.ToString(),
                    "DISTANCE_METRIC", "COSINE");
            }
            catch (RedisException)
            {
                // "Index already exists" is expected after the first run — not an error.
            }
            _indexCreated = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    // Parses the FT.SEARCH multi-bulk response into a CacheResult.
    // FT.SEARCH result structure: [count, key1, [field, value, ...], key2, ...]
    // With RETURN 2 response score, the field array contains only those two fields.
    private Option<CacheResult> ParseSearchResult(RedisResult result)
    {
        if (result.Resp2Type != ResultType.Array) return Option<CacheResult>.None;

#pragma warning disable CS8600
        RedisResult[]? items = (RedisResult[])result;
#pragma warning restore CS8600

        // items[0] = total count, items[1] = key, items[2] = field array.
        // Need at least 3 items and a non-zero count to have a result.
        if (items is null || items.Length < 3 || (long)items[0] == 0) return Option<CacheResult>.None;

        // CS8600: StackExchange.Redis explicit cast operator lacks nullable annotation;
        // the null check on the next line guards against the null case at runtime.
#pragma warning disable CS8600
        RedisResult[]? fields = (RedisResult[])items[2];
#pragma warning restore CS8600
        if (fields is null) return Option<CacheResult>.None;
        string? response = null;
        var distance = float.MaxValue;

        // Fields come back as alternating [name, value, name, value, ...] pairs.
        for (var i = 0; i < fields.Length - 1; i += 2)
        {
            var name  = (string?)fields[i];
            var value = (string?)fields[i + 1];

            if (name == "response") response = value;
            else if (name == "score" && float.TryParse(value, out var d)) distance = d;
        }

        if (response is null) return Option<CacheResult>.None;

        // RediSearch COSINE returns distance = 1 - cosine_similarity.
        // Convert to similarity so threshold comparisons are intuitive (higher = more similar).
        var similarity = 1f - distance;

        if (similarity < _options.SimilarityThreshold) return Option<CacheResult>.None;

        return Option<CacheResult>.Some(new CacheResult
        {
            SerializedResponse = response,
            Similarity = similarity
        });
    }

    // Converts a float[] embedding to the raw byte array Redis vector search requires.
    // Vectors are stored as contiguous 32-bit floats in little-endian byte order.
    private static byte[] VectorToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    // Produces a short, stable hex string to use as a Redis key suffix.
    // SHA-256 ensures uniqueness even for very similar intent strings.
    private static string ComputeHash(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
