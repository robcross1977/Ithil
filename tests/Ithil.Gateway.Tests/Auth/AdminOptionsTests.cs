using AwesomeAssertions;
using Ithil.Gateway.Auth;

namespace Ithil.Gateway.Tests.Auth;

/// <summary>
/// Pins the production defaults for AdminOptions.
/// ApiKey is intentionally empty by default — the endpoint is disabled until a key is injected.
/// TokenExpiryDays controls the lifetime of issued admin JWTs; 90 days is the safe default.
/// </summary>
public sealed class AdminOptionsTests
{
    [Fact]
    public void ApiKey_DefaultsToEmptyString()
    {
        new AdminOptions().ApiKey.Should().BeEmpty();
    }

    [Fact]
    public void TokenExpiryDays_DefaultsTo90()
    {
        new AdminOptions().TokenExpiryDays.Should().Be(90);
    }
}
