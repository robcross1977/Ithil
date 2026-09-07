using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;

namespace Ithil.Gateway.Tests;

public class HealthCheckTests
{
    [Fact]
    public async Task HealthEndpoint_LiveReturns200_WithNoAuth()
    {
        await using var app = await StartHostAsync(configure: null);

        var response = await app.GetTestClient().GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_ReadyReturns200_WhenAllReadyChecksPass()
    {
        await using var app = await StartHostAsync(hc => hc
            .AddCheck("probe", () => HealthCheckResult.Healthy(), tags: ["ready"]));

        var response = await app.GetTestClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_ReadyReturns503_WhenAReadyCheckFails()
    {
        await using var app = await StartHostAsync(hc => hc
            .AddCheck("probe", () => HealthCheckResult.Unhealthy("nope"), tags: ["ready"]));

        var response = await app.GetTestClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task HealthEndpoint_LiveIgnoresReadyChecks()
    {
        // Critical safety property: a failing readiness check must NOT fail liveness,
        // or Kubernetes will restart pods during transient dependency outages.
        await using var app = await StartHostAsync(hc => hc
            .AddCheck("probe", () => HealthCheckResult.Unhealthy("redis down"), tags: ["ready"]));

        var response = await app.GetTestClient().GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthAlias_Returns200_WhenAllReadyChecksPass()
    {
        await using var app = await StartHostAsync(hc => hc
            .AddCheck("probe", () => HealthCheckResult.Healthy(), tags: ["ready"]));

        var response = await app.GetTestClient().GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthAlias_Returns503_WhenAReadyCheckFails()
    {
        await using var app = await StartHostAsync(hc => hc
            .AddCheck("probe", () => HealthCheckResult.Unhealthy("nope"), tags: ["ready"]));

        var response = await app.GetTestClient().GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    // Spins up a minimal host that mirrors the real endpoint mappings from Program.cs.
    // The caller supplies the readiness checks under test.
    private static async Task<WebApplication> StartHostAsync(
        Action<IHealthChecksBuilder>? configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var hc = builder.Services.AddHealthChecks();
        configure?.Invoke(hc);

        var app = builder.Build();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
