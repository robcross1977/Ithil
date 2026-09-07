using AwesomeAssertions;
using Ithil.Gateway.Options;

namespace Ithil.Gateway.Tests.Options;

/// <summary>
/// Pins the production default for ShutdownOptions.
/// 25 seconds is chosen to sit just inside Kubernetes' default terminationGracePeriodSeconds (30s),
/// giving .NET 5 seconds to exit cleanly before the pod is force-killed.
/// </summary>
public sealed class ShutdownOptionsTests
{
    [Fact]
    public void TimeoutSeconds_DefaultsTo25()
    {
        new ShutdownOptions().TimeoutSeconds.Should().Be(25);
    }
}
