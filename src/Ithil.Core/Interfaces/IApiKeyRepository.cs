using LanguageExt;

namespace Ithil.Core.Interfaces;

/// <summary>
/// Stores and looks up API keys for agent authentication.
/// Keys are stored as SHA-256 hashes — plaintext is never persisted.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>
    /// Returns the agentId associated with the given hashed key, or None if not found.
    /// </summary>
    Task<Option<string>> FindByHashedKeyAsync(string hashedKey);

    /// <summary>
    /// Generates a new API key for the given agent, stores it hashed, and returns the plaintext key.
    /// The plaintext is only available at creation time.
    /// </summary>
    Task<string> CreateAsync(string agentId);

    /// <summary>
    /// Removes the entry for the given hashed key.
    /// No-op if the hash is not found.
    /// </summary>
    Task DeleteAsync(string hashedKey);
}
