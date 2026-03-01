using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using System.Net;

namespace Ithil.Gateway.Tests;

public class HealthCheckTests
{
    [Fact]
    public async Task HealthCheck_Returns200_WithNoAuth()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.MapHealthChecks("/health");

        await app.StartAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
