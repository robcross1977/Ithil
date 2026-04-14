using Microsoft.AspNetCore.Authorization;

namespace Ithil.Gateway.Management;

/// <summary>
/// Registers authorization policies for management and agent routes.
/// </summary>
public static class ManagementAuthPolicy
{
    public const string PolicyName = "ManagementPolicy";
    public const string AgentPolicyName = "AgentPolicy";

    /// <summary>
    /// Requires an authenticated user with a JWT claim of scope: admin.
    /// Agent tokens (no admin scope) are rejected by this policy.
    /// </summary>
    public static AuthorizationBuilder AddManagementPolicy(this AuthorizationBuilder builder) =>
        builder.AddPolicy(PolicyName, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("scope", "admin"));

    /// <summary>
    /// Requires an authenticated user with an agent_id claim.
    /// Admin-scoped tokens are rejected by this policy, even if they also include agent_id.
    /// </summary>
    public static AuthorizationBuilder AddAgentPolicy(this AuthorizationBuilder builder) =>
        builder.AddPolicy(AgentPolicyName, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("agent_id")
            .RequireAssertion(context => !context.User.HasClaim(claim =>
                claim.Type == "scope" && claim.Value == "admin")));
}
