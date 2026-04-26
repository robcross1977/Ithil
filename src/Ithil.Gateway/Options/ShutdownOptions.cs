namespace Ithil.Gateway.Options;

/// <summary>
/// Host shutdown timing. <see cref="TimeoutSeconds"/> controls how long the process
/// waits for in-flight requests and <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>
/// workers to drain before forcing exit.
/// </summary>
/// <remarks>
/// Must be less than the Kubernetes <c>terminationGracePeriodSeconds</c> (default 30s) so
/// .NET shuts down cleanly before the pod is force-killed. The recommended relationship is:
/// <c>terminationGracePeriodSeconds = TimeoutSeconds + 5</c>.
/// </remarks>
public class ShutdownOptions
{
    /// <summary>Seconds to wait for graceful shutdown. Default is 25.</summary>
    public int TimeoutSeconds { get; set; } = 25;
}
