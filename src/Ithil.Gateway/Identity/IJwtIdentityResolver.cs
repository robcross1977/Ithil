using LanguageExt;

namespace Ithil.Gateway.Identity;

internal interface IJwtIdentityResolver
{
    Task<Option<string>> TryResolveAsync(HttpContext context);
}
