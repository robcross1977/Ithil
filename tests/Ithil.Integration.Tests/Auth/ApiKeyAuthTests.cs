using AwesomeAssertions;
using Ithil.Core.Models;
using Ithil.Integration.Tests.Fixtures;
using System.Net;

namespace Ithil.Integration.Tests.Auth;

/// <summary>
/// Integration tests for API key authentication through the full gateway pipeline.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class ApiKeyAuthTests(GatewayFixture fixture)
{
    [Fact]
    public async Task ValidApiKey_ActiveAgent_Returns200()
    {
        var agentId = $"apikey-valid-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });
        var apiKey = await fixture.ApiKeyRepo.CreateAsync(agentId);

        var response = await GetAsync(apiKey);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownApiKey_Returns401()
    {
        var response = await GetAsync("ithil_live_notavalidkey");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NoAuthHeader_Returns401()
    {
        var response = await fixture.Client.GetAsync("/api/TestTool", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApiKeyForInactiveAgent_Returns401()
    {
        var agentId = $"apikey-inactive-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = false,
        });
        var apiKey = await fixture.ApiKeyRepo.CreateAsync(agentId);

        var response = await GetAsync(apiKey);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> GetAsync(string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/TestTool");
        request.Headers.Add("X-Api-Key", apiKey);
        return fixture.Client.SendAsync(request);
    }
}
