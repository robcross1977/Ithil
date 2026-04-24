using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Ithil.Gateway.Health;

/// <summary>
/// Readiness check: pings Redis to confirm the gateway's Redis-backed shared
/// state is reachable. A failure here means Redis-dependent features such as
/// budget enforcement and semantic caching cannot function — traffic should be
/// diverted until Redis recovers. Not used as a liveness check: a transient
/// Redis outage must not cause Kubernetes to restart the pod.
/// </summary>
public class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync(CommandFlags.None);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException)
        {
            // Probe timed out or host is shutting down — not a Redis failure.
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                description: "Redis ping failed",
                exception: ex);
        }
    }
}
