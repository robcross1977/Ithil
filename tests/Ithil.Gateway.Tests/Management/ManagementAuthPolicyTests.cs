using FluentAssertions;
using Ithil.Gateway.Management;

namespace Ithil.Gateway.Tests.Management;

/// <summary>
/// Pins the constant string values on ManagementAuthPolicy.
/// These strings are referenced in both the policy registration and in RequireAuthorization calls —
/// they must stay in sync. Pinning them here ensures a rename shows up as a failing test.
/// </summary>
public sealed class ManagementAuthPolicyTests
{
    [Fact]
    public void PolicyName_IsExpectedValue()
    {
        ManagementAuthPolicy.PolicyName.Should().Be("ManagementPolicy");
    }

    [Fact]
    public void AgentPolicyName_IsExpectedValue()
    {
        ManagementAuthPolicy.AgentPolicyName.Should().Be("AgentPolicy");
    }
}
