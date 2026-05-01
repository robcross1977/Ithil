using FluentAssertions;
using Ithil.Core.Models;
using Ithil.Integration.Tests.Fixtures;
using System.Net;
using System.Net.Http.Headers;

namespace Ithil.Integration.Tests.Auth;

/// <summary>
/// Integration tests for JWT Bearer authentication through the full gateway pipeline.
/// Each test creates a unique agent to avoid state leaking between cases.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class JwtAuthTests(GatewayFixture fixture)
{
    [Fact]
    public async Task ValidJwt_ActiveAgent_Returns200()
    {
        var agentId = $"jwt-valid-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });

        var response = await GetAsync(fixture.CreateAgentJwt(agentId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NoAuthHeader_Returns401()
    {
        var response = await fixture.Client.GetAsync("/api/TestTool", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtSignedWithWrongKey_Returns401()
    {
        var agentId = $"jwt-wrongkey-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });
        var wrongKeyJwt = fixture.CreateAgentJwt(agentId, signingKey: "wrong-key-that-is-long-enough-32b!!");

        var response = await GetAsync(wrongKeyJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredJwt_Returns401()
    {
        var agentId = $"jwt-expired-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });
        var expiredJwt = fixture.CreateAgentJwt(agentId, expires: DateTimeOffset.UtcNow.AddHours(-1));

        var response = await GetAsync(expiredJwt);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtWithUnknownAgentId_Returns401()
    {
        // Agent exists in the JWT but NOT in the config repo — identity resolution returns None.
        var jwt = fixture.CreateAgentJwt("agent-that-does-not-exist");

        var response = await GetAsync(jwt);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtForInactiveAgent_Returns401()
    {
        var agentId = $"jwt-inactive-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = false,
        });

        var response = await GetAsync(fixture.CreateAgentJwt(agentId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> GetAsync(string bearerToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/TestTool");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return fixture.Client.SendAsync(request);
    }
}
