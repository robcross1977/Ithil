using Microsoft.AspNetCore.Authorization;

namespace Ithil.Dashboard.Auth;

/// <summary>
/// Registers the dashboard authorization policy, requiring an admin-scoped
/// session cookie. Agent JWTs without admin scope are rejected.
/// </summary>
public static class DashboardAuthPolicy
{
    public const string PolicyName = "DashboardPolicy";
    public const string CookieScheme = "DashboardCookie";

    /// <summary>
    /// Requires an authenticated user with a scope: admin claim,
    /// validated against the dashboard cookie scheme only.
    /// </summary>
    public static AuthorizationBuilder AddDashboardPolicy(
        this AuthorizationBuilder builder) =>
            builder.AddPolicy(PolicyName, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "admin")
                .AddAuthenticationSchemes(CookieScheme));
}
