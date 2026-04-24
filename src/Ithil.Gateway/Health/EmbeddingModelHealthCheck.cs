using Ithil.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ithil.Gateway.Health;

/// <summary>
/// Readiness check: confirms the ONNX embedding model loaded successfully at startup.
/// The semantic cache cannot service requests until this is true, so routing
/// traffic to an unready pod would cause every cache lookup to fail.
/// IEmbeddingService is eagerly resolved at startup (alongside ITokenCounter).
/// If model initialization fails the application will fail to start and Kubernetes
/// will restart the pod; in practice this check will report unhealthy only in the
/// narrow window between process start and eager resolution completing.
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
