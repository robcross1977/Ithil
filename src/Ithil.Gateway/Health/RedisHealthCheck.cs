using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Ithil.Gateway.Health;

/// <summary>
/// Readiness check: pings Redis to confirm the gateway's shared-state backend
/// is reachable. A failure here means budget enforcement, semantic cache, and
/// agent identity lookups cannot function — traffic should be diverted until
/// Redis recovers. Not used as a liveness check: a transient Redis outage
/// must not cause Kubernetes to restart the pod.
/// </summary>
public class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                description: "Redis ping failed",
                exception: ex);
        }
    }
}
