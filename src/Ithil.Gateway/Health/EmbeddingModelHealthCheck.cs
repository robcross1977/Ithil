using Ithil.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ithil.Gateway.Health;

/// <summary>
/// Readiness check: confirms the ONNX embedding model has finished loading.
/// The semantic cache cannot service requests until this is true, so routing
/// traffic to an unready pod would cause every cache lookup to fail.
/// IEmbeddingService is eagerly resolved at startup (alongside ITokenCounter),
/// so this check reflects real startup state rather than first-request latency.
/// A false result does not restart the pod — the model loaded once and a
/// restart would hit the same failure.
/// </summary>
public class EmbeddingModelHealthCheck(IEmbeddingService embedding) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(embedding.IsReady
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Embedding model is not loaded"));
}
