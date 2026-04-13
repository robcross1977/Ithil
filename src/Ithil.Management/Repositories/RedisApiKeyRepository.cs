using System.Security.Cryptography;
using Ithil.Core.Crypto;
using Ithil.Core.Interfaces;
using LanguageExt;
using StackExchange.Redis;

namespace Ithil.Management.Repositories;

/// <summary>
/// Redis-backed API key store. Persists key hashes in a Redis Hash at key <c>ithil:apikeys</c>.
/// </summary>
public class RedisApiKeyRepository(IConnectionMultiplexer redis) : IApiKeyRepository
{
    private const string HashKey = "ithil:apikeys";
    private readonly IDatabase _db = redis.GetDatabase();

    /// <summary>
    /// Returns the agentId for the given hashed key, or None if not found.
    /// </summary>
    public async Task<Option<string>> FindByHashedKeyAsync(string hashedKey)
    {
        var value = await _db.HashGetAsync(HashKey, hashedKey);
        return value.HasValue
            ? Option<string>.Some(value!)
            : Option<string>.None;
    }

    /// <summary>
    /// Generates an ithil_live_ prefixed key, stores its hash in Redis, and returns the plaintext.
    /// </summary>
    public async Task<string> CreateAsync(string agentId)
    {
        var plaintext = GenerateKey();
        await _db.HashSetAsync(HashKey, ApiKeyHasher.Hash(plaintext), agentId);
        return plaintext;
    }

    /// <summary>
    /// Removes the entry for the given hashed key. No-op if not found.
    /// </summary>
    public Task DeleteAsync(string hashedKey) =>
        _db.HashDeleteAsync(HashKey, hashedKey);

    // Generates: ithil_live_ + 32 random bytes as lowercase hex (75 chars total)
    private static string GenerateKey() =>
        "ithil_live_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
                               .ToLowerInvariant();
}
