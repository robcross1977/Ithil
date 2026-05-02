using System.Collections.Concurrent;
using System.Security.Cryptography;
using Ithil.Core.Crypto;
using Ithil.Core.Interfaces;
using LanguageExt;

namespace Ithil.Management.Repositories;

/// <summary>
/// In-memory API key store backed by a concurrent dictionary.
/// All data is lost when the process exits. Use for development or testing only.
/// </summary>
public class InMemoryApiKeyRepository : IApiKeyRepository
{
    // Stores hash → agentId. The plaintext key is never kept.
    private readonly ConcurrentDictionary<string, string> _store = new();

    /// <summary>
    /// Returns the agentId for the given hashed key, or None if not found.
    /// </summary>
    public Task<Option<string>> FindByHashedKeyAsync(string hashedKey) =>
        Task.FromResult(
            _store.TryGetValue(hashedKey, out var agentId)
                ? Option<string>.Some(agentId)
                : Option<string>.None
        );

    /// <summary>
    /// Generates an ithil_live_ prefixed key, stores its hash, and returns the plaintext.
    /// </summary>
    public Task<string> CreateAsync(string agentId)
    {
        var plaintext = GenerateKey();
        var hash = ApiKeyHasher.Hash(plaintext);
        _store[hash] = agentId;
        return Task.FromResult(plaintext);
    }

    /// <summary>
    /// Removes the entry for the given hashed key. No-op if not found.
    /// </summary>
    public Task DeleteAsync(string hashedKey)
    {
        _store.TryRemove(hashedKey, out _);
        return Task.CompletedTask;
    }

    // Generates: ithil_live_ + 32 random bytes as lowercase hex (75 chars total)
    private static string GenerateKey() =>
        "ithil_live_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
                               .ToLowerInvariant();
}
