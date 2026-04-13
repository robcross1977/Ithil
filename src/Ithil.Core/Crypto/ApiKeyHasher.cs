using System.Security.Cryptography;
using System.Text;

namespace Ithil.Core.Crypto;

/// <summary>
/// Single source of truth for API key hashing.
/// All components that hash or compare API keys must use this class.
/// </summary>
public static class ApiKeyHasher
{
    /// <summary>
    /// Returns the SHA-256 hex digest of the given raw key, lowercase.
    /// The plaintext key is never stored — only this hash.
    /// </summary>
    public static string Hash(string rawKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)))
               .ToLowerInvariant();
}
