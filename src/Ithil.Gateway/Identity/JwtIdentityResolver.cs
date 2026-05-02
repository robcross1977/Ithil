using LanguageExt;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ithil.Gateway.Identity;

/// <summary>
/// Resolves an agent ID from a Bearer JWT in the Authorization header.
/// Returns None if the header is absent, the token is expired, or the signature is invalid.
/// </summary>
public class JwtIdentityResolver(TokenValidationParameters validationParameters) : IJwtIdentityResolver
{
    private readonly TokenValidationParameters _validationParameters = validationParameters;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>
    /// Validates the Bearer token and extracts the agent_id claim.
    /// Returns None for any validation failure - callers receive no detail about why.
    /// </summary>
    public async Task<Option<string>> TryResolveAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if(!authHeader.StartsWith("Bearer ")) return Option<string>.None;

        var token = authHeader["Bearer ".Length..];
        var result = await _handler.ValidateTokenAsync(token, _validationParameters);

        if(!result.IsValid) return Option<string>.None;

        return result.Claims.TryGetValue("agent_id", out var agentId)
            ? Option<string>.Some(agentId.ToString()!)
            :Option<string>.None;
    }
}
