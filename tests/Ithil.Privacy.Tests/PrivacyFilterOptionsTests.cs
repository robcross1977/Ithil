using FluentAssertions;

namespace Ithil.Privacy.Tests;

/// <summary>
/// Pins production defaults for PrivacyFilterOptions.
/// </summary>
public sealed class PrivacyFilterOptionsTests
{
    [Fact]
    public void CustomRules_DefaultsToEmptyList()
    {
        // An empty list means only the built-in rules (email, SSN, credit card) apply
        // until the operator explicitly configures additions in appsettings.
        new PrivacyFilterOptions().CustomRules.Should().BeEmpty();
    }
}
