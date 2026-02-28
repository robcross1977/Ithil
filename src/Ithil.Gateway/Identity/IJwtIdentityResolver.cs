using LanguageExt;
using Microsoft.AspNetCore.Http;

namespace Ithil.Gateway.Identity;

internal interface IJwtIdentityResolver
{
    Task<Option<string>> TryResolveAsync(HttpContext context);
}
