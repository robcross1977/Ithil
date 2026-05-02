using Ithil.Core.Crypto;
using Ithil.Core.Interfaces;
using LanguageExt;

namespace Ithil.Gateway.Identity;

/// <summary>
/// Resolves an agent ID from an API key in the X-Api-Key header.
/// The raw key is never stored or logged — only its SHA-256 hash is used for lookup.
/// </summary>
public class ApiKeyIdentityResolver(IApiKeyRepository repo) : IApiKeyIdentityResolver
{
    private readonly IApiKeyRepository _repo = repo;

    /// <summary>
    /// Hashes the raw key and looks it up in the repository.
    /// Returns None if the header is absent or the key is not found.
    /// </summary>
    public async Task<Option<string>> TryResolveAsync(HttpContext context)
    {
        var rawKey = context.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(rawKey)) return Option<string>.None;

        return await _repo.FindByHashedKeyAsync(ApiKeyHasher.Hash(rawKey));
    }
}
