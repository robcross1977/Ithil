using LanguageExt;

namespace Ithil.Gateway.Identity;

internal interface IApiKeyIdentityResolver
{
    Task<Option<string>> TryResolveAsync(HttpContext context);
}
