using Ithil.Core.Interfaces;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace Ithil.Gateway.Identity;

/// <summary>
/// Resolvfes an agent ID from an API key in the X-Api-Key header.
/// The ram key is never stored or logged - only its SHA-256 hash is used for lookup.t ≥≤
/// </summary>
public class ApiKeyIdentityResolver(IApiKeyRepository repo) : IApiKeyIdentityResolver
{
    private readonly IApiKeyRepository _repo = repo;

    /// <summary>
    /// Hashes the raw key and looks it up in the repository.
    /// Returns None is the header is absent or the key is not found.
    /// </summary>
    public async Task<Option<string>> TryResolveAsync(HttpContext context)
    {
        var rawKey = context.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(rawKey)) return Option<string>.None;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
        return await _repo.FindByHashedKeyAsync(hash);
    }
}
