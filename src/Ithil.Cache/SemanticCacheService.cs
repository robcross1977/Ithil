using Ithil.Core.Enums;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ithil.Cache;

/// <summary>
/// Answers tool calls from a Redis cache by meaning rather than exact parameters.
///
/// How it works end to end:
///   When a tool call arrives, we turn it into a meaning vector (embedding) and ask Redis
///   to find the stored entry whose vector is closest to it. If that match is similar enough
///   (above the configured threshold), we return its cached response and skip the real API call.
///   On a miss, the caller makes the real request and calls SetAsync to store the result for
///   next time.
///
/// What gets stored in Redis:
///   Each cached entry is a Redis "hash" — think of it as a small dictionary stored under
///   one key — with two fields:
///     - response:  the JSON-serialised tool response (what we'll return on a hit)
///     - embedding: the raw bytes of the meaning vector for the original tool call
///
///   RediSearch — a Redis extension that adds search capability on top of the key-value store —
///   maintains a vector index over those embedding fields. Without RediSearch, Redis can only
///   look things up by exact key. The index teaches Redis how to answer "which stored entry
///   has a vector most similar to this query vector?"
///
/// Failure handling:
///   Redis failures in TryGetAsync are handled according to the configured FailurePolicy.
///   Non-Redis errors (model unavailable, parse failures) are always treated as cache misses.
///   All failures in SetAsync are silently swallowed — a failed write is never fatal.
/// </summary>
public class SemanticCacheService(
    IEmbeddingService embedder,
    IDatabase redis,
    SemanticCacheOptions options) : ISemanticCache
{
    private readonly IEmbeddingService _embedder = embedder;
    private readonly IDatabase _redis = redis;
    private readonly SemanticCacheOptions _options = options;

    // Whether the RediSearch vector index has been created yet.
    // Index creation is idempotent (safe to call repeatedly) but we only need to attempt it
    // once per instance — _indexCreated lets us skip the Redis round-trip on every subsequent call.
    private bool _indexCreated;

    // The lock that makes index creation thread-safe.
    // SemaphoreSlim(1,1) is an async-compatible mutex — only one caller enters at a time.
    // We use double-checked locking: check _indexCreated before acquiring the lock (fast path
    // once the index exists) and again inside the lock (in case two concurrent requests both
    // saw false and are queued up — only the first should create the index).
    private readonly SemaphoreSlim _indexLock = new(1, 1);

    // The name of the RediSearch index. All cache entries use the "cache:" key prefix so
    // the index only covers cache entries, not other Redis keys in the same database.
    private const string IndexName = "ithil-cache-idx";
    private const string KeyPrefix = "cache:";
    private const int EmbeddingDims = 384;

    /// <summary>
    /// Looks up a semantically similar cached response for the given tool call.
    /// Returns None on a miss, on any Redis failure (FailOpen), or on any non-Redis error.
    /// Callers should always proceed to the real downstream service when None is returned.
    /// </summary>
    public async Task<Option<CacheResult>> TryGetAsync(string toolName, object parameters)
    {
        try
        {
            await EnsureIndexAsync();

            // Turn the tool call into a stable string, then embed it into a meaning vector.
            var intent = IntentSerializer.Serialize(toolName, parameters);
            var vector = await _embedder.EmbedAsync(intent);

            // Redis vector search requires the query vector as raw bytes, not a C# float[].
            var vectorBytes = VectorToBytes(vector);

            // Ask RediSearch for the single nearest stored entry to our query vector.
            //
            // Breaking down the query string "*=>[KNN 1 @embedding $vec AS score]":
            //   *            = match all documents (no text pre-filter — search everything)
            //   =>           = "then apply this vector operation to those results"
            //   KNN 1        = K-Nearest Neighbors: find the 1 closest stored entry
            //   @embedding   = look in the field named "embedding"
            //   $vec         = using this query vector (passed separately via PARAMS below)
            //   AS score     = name the resulting distance value "score" so we can read it back
            //
            // "PARAMS" "2" "vec" vectorBytes:
            //   Redis expects a count before the parameter list — "2" means "2 tokens follow:
            //   a name ('vec') and a value (the byte array)". This is a Redis protocol convention.
            //
            // "RETURN" "2" "response" "score":
            //   Only return these 2 named fields. Without this Redis would return every field
            //   including the raw embedding bytes, which is wasteful and slow.
            //
            // "DIALECT" "2": required to enable the vector search query syntax above.
            var result = await _redis.ExecuteAsync("FT.SEARCH",
                IndexName,
                "*=>[KNN 1 @embedding $vec AS score]",
                "PARAMS", "2", "vec", vectorBytes,
                "RETURN", "2", "response", "score",
                "DIALECT", "2");

            return ParseSearchResult(result);
        }
        catch (RedisException) when (_options.FailurePolicy == RedisFailurePolicy.FailOpen)
        {
            // Redis is unavailable and we're configured to fail open — treat as a cache miss
            // so the request can still proceed to the real downstream service.
            return Option<CacheResult>.None;
        }
        catch (Exception ex) when (ex is not RedisException)
        {
            // Non-Redis errors (embedding model unavailable, parse failure, etc.) are always
            // treated as misses regardless of FailurePolicy — these are not Redis availability
            // events and do not indicate the cache is down.
            return Option<CacheResult>.None;
        }
    }

    /// <summary>
    /// Stores a tool response in the cache so future similar calls can be served from here.
    /// Failures are silently swallowed — a failed write must never fail the original request.
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

            // Use a short hash of the intent string as the Redis key suffix.
            // This keeps keys short, unique, and deterministic — the same intent always
            // maps to the same key, so repeated calls overwrite rather than duplicate.
            var key = $"{KeyPrefix}{ComputeHash(intent)}";

            // HSET creates (or overwrites) a Redis hash at `key` with the given fields.
            // A Redis hash is a dictionary stored under one key — here we store two fields:
            //   response:  the JSON response body to return on a future cache hit
            //   embedding: the raw float32 bytes of the meaning vector, required by RediSearch
            //              to include this entry in vector similarity searches
            await _redis.ExecuteAsync("HSET",
                key,
                "response", serializedResponse,
                "embedding", vectorBytes);

            // Set an expiry so stale entries don't accumulate indefinitely.
            // EXPIRE takes seconds, so we convert the TimeSpan.
            await _redis.ExecuteAsync("EXPIRE", key, (long)ttl.TotalSeconds);
        }
        catch (Exception)
        {
            // Swallow all failures — cache write errors are non-fatal.
        }
    }

    // Creates the RediSearch vector index the first time it is needed.
    // RediSearch (the "FT" prefix stands for "Full Text", the module's original focus)
    // extends Redis with the ability to query by content. The vector index specifically
    // enables the KNN nearest-neighbor search used in TryGetAsync.
    //
    // Uses double-checked locking — see the _indexLock field comment above.
    private async Task EnsureIndexAsync()
    {
        if (_indexCreated) return; // fast path — index already exists, skip the lock entirely

        await _indexLock.WaitAsync();
        try
        {
            if (_indexCreated) return; // another caller created it while we were waiting
            try
            {
                // FT.CREATE defines the schema of the index — what fields to index and how.
                //
                // ON HASH:          index Redis hashes (as opposed to JSON or stream types)
                // PREFIX 1 cache:   only index keys that start with "cache:" — ignores all
                //                   other Redis keys in the database
                // SCHEMA:           everything after this defines the indexed fields
                //
                //   response TEXT:  the cached response body, indexed for full-text search
                //   score NUMERIC:  the distance field returned by KNN queries
                //
                //   embedding VECTOR FLAT 6 ...:
                //     VECTOR = this field holds an embedding vector
                //     FLAT   = use a brute-force index: check every stored entry one by one
                //              and pick the closest. Always accurate, but gets slow above
                //              ~100k entries. The alternative (HNSW) is a graph-based
                //              approximation that scales better but can occasionally miss
                //              the true nearest neighbor.
                //     6      = "6 configuration values follow" (Redis counts before listing)
                //     TYPE FLOAT32        = each dimension is a 4-byte float
                //     DIM 384             = matches all-MiniLM-L6-v2's output size
                //     DISTANCE_METRIC COSINE = measure the angle between vectors
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
                // RediSearch throws an error if the index already exists (e.g. after an app
                // restart, or when multiple instances start simultaneously). That's fine —
                // we just need to confirm the index is there, not that we created it.
            }
            _indexCreated = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    // Parses the raw Redis response from FT.SEARCH into a CacheResult.
    //
    // Redis returns results as a nested array. With RETURN 2 response score, the structure is:
    //
    //   [
    //     1,                    ← total number of matches found
    //     "cache:ab3f...",      ← the Redis key of the matching entry
    //     [                     ← the fields we asked for
    //       "response", "{\"quantity\":42}",
    //       "score",    "0.030000"
    //     ]
    //   ]
    //
    // items[0] = count, items[1] = key, items[2] = field array.
    // Fields come back as alternating [name, value, name, value, ...] pairs.
    private Option<CacheResult> ParseSearchResult(RedisResult result)
    {
        if (result.Resp2Type != ResultType.Array) return Option<CacheResult>.None;

#pragma warning disable CS8600
        RedisResult[]? items = (RedisResult[])result;
#pragma warning restore CS8600

        // Need at least 3 items (count + key + fields) and a non-zero count to have a hit.
        if (items is null || items.Length < 3 || (long)items[0] == 0) return Option<CacheResult>.None;

        // CS8600: StackExchange.Redis's explicit cast operator lacks a nullable annotation;
        // the null check on the next line guards against the null case at runtime.
#pragma warning disable CS8600
        RedisResult[]? fields = (RedisResult[])items[2];
#pragma warning restore CS8600
        if (fields is null) return Option<CacheResult>.None;

        string? response = null;
        var distance = float.MaxValue;

        for (var i = 0; i < fields.Length - 1; i += 2)
        {
            var name  = (string?)fields[i];
            var value = (string?)fields[i + 1];

            if (name == "response") response = value;
            else if (name == "score" && float.TryParse(value, out var d)) distance = d;
        }

        if (response is null) return Option<CacheResult>.None;

        // RediSearch's COSINE metric returns distance = 1 - cosine_similarity.
        // Distance 0 = identical vectors; distance 1 = completely unrelated.
        // We flip it to similarity (higher = more similar) so the threshold comparison
        // is intuitive: "is this result similar enough?" means "is the number high enough?"
        var similarity = 1f - distance;

        if (similarity < _options.SimilarityThreshold) return Option<CacheResult>.None;

        return Option<CacheResult>.Some(new CacheResult
        {
            SerializedResponse = response,
            Similarity = similarity
        });
    }

    // Converts a float[] embedding vector to the raw byte array that Redis vector search requires.
    //
    // RediSearch stores and compares vectors as raw binary — it reads them directly as arrays
    // of 32-bit floats in memory. Sending them as a JSON array ("[0.1, 0.4, ...]") would work
    // for storage but Redis wouldn't be able to include them in vector similarity searches.
    //
    // Buffer.BlockCopy reinterprets the float[] as byte[] by copying raw memory — it does not
    // convert the numbers, just copies the bits. This is fast and produces the exact layout
    // (contiguous 32-bit floats in little-endian order) that RediSearch expects.
    private static byte[] VectorToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    // Produces a short, stable hex string to use as the Redis key suffix for a cache entry.
    //
    // SHA-256 hashes the intent string into a fixed-size digest, then we take the first
    // 16 hex characters (= 64 bits of the hash). 64 bits gives 2^64 possible values —
    // effectively collision-proof for any realistic cache size, while keeping keys short.
    // The same intent string always produces the same key, so writing the same call twice
    // overwrites the existing entry rather than creating a duplicate.
    private static string ComputeHash(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
