using Microsoft.AspNetCore.Authorization;

namespace Ithil.Gateway.Management;

/// <summary>
/// Registers the ManagementPolicy authorization policy.
/// Requires an authenticated user with a JWT claim of scope: admin.
/// Agent tokens (no admin scope) are rejected by this policy.
/// </summary>
public static class ManagementAuthPolicy
{
    public const string PolicyName = "ManagementPolicy";

    public static AuthorizationBuilder AddManagementPolicy(this AuthorizationBuilder builder) =>
        builder.AddPolicy(PolicyName, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("scope", "admin"));
}
