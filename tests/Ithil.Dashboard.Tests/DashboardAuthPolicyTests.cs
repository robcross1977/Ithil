using AwesomeAssertions;
using Ithil.Dashboard.Auth;

namespace Ithil.Dashboard.Tests;

/// <summary>
/// Pins the DashboardAuthPolicy constant values.
/// PolicyName is used in both AddDashboardPolicy and RequireAuthorization — they must match.
/// CookieScheme is referenced by AddCookie, SignInAsync, SignOutAsync, and AddAuthenticationSchemes — all must agree.
/// </summary>
public sealed class DashboardAuthPolicyTests
{
    [Fact]
    public void PolicyName_IsExpectedValue()
    {
        DashboardAuthPolicy.PolicyName.Should().Be("DashboardPolicy");
    }

    [Fact]
    public void CookieScheme_IsExpectedValue()
    {
        DashboardAuthPolicy.CookieScheme.Should().Be("DashboardCookie");
    }
}
