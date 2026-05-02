using FluentAssertions;
using Ithil.Core.Interfaces;
using Ithil.Gateway.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;

namespace Ithil.Gateway.Tests.Health;

public class EmbeddingModelHealthCheckTests
{
    [Fact]
    public async Task EmbeddingModelHealthCheck_ReturnsHealthy_WhenModelIsReady()
    {
        var embedding = Substitute.For<IEmbeddingService>();
        embedding.IsReady.Returns(true);

        var check = new EmbeddingModelHealthCheck(embedding);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task EmbeddingModelHealthCheck_ReturnsUnhealthy_WhenModelNotReady()
    {
        var embedding = Substitute.For<IEmbeddingService>();
        embedding.IsReady.Returns(false);

        var check = new EmbeddingModelHealthCheck(embedding);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
