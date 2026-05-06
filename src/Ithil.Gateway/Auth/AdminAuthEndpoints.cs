using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Ithil.Gateway.Auth;

/// <summary>
/// Maps the admin token bootstrap endpoint.
/// </summary>
public static class AdminAuthEndpoints
{
    /// <summary>
    /// Adds POST /auth/admin/token. Returns 404 if no admin API key is configured,
    /// so the endpoint is invisible in environments that don't need it.
    /// </summary>
    public static IEndpointRouteBuilder MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/admin/token", (AdminTokenRequest request, AdminOptions options, IConfiguration config) =>
        {
            // Endpoint is disabled if no key is configured — return 404, not 401, to avoid
            // revealing that the endpoint exists at all.
            if (string.IsNullOrEmpty(options.ApiKey))
                return Results.NotFound();

            // Constant-time comparison prevents timing attacks on the key value.
            // We check length first (which does reveal length, but the key format is fixed
            // and an attacker would need authenticated network access to probe anyway).
            var configBytes  = Encoding.UTF8.GetBytes(options.ApiKey);
            var requestBytes = Encoding.UTF8.GetBytes(request.ApiKey ?? string.Empty);
            if (configBytes.Length != requestBytes.Length ||
                !CryptographicOperations.FixedTimeEquals(configBytes, requestBytes))
                return Results.Unauthorized();

            var jwt       = config.GetSection("Ithil:Jwt");
            var key       = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!));
            var expiresAt = DateTime.UtcNow.AddDays(options.TokenExpiryDays);
            var token     = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Claims            = new Dictionary<string, object> { { "scope", "admin" } },
                Expires           = expiresAt,
                Issuer            = jwt["Issuer"],
                Audience          = jwt["Audience"],
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            });

            return Results.Ok(new { token, expiresAt });
        }).AllowAnonymous();

        return app;
    }
}

/// <summary>Request body for POST /auth/admin/token.</summary>
public record AdminTokenRequest(string ApiKey);
