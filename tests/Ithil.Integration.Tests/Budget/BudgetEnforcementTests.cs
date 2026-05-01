using FluentAssertions;
using Ithil.Core.Models;
using Ithil.Integration.Tests.Fixtures;
using System.Net;
using System.Net.Http.Headers;

namespace Ithil.Integration.Tests.Budget;

/// <summary>
/// Integration tests for Redis-backed token budget enforcement.
/// Each test uses a unique agent so budget state never bleeds across cases.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class BudgetEnforcementTests(GatewayFixture fixture)
{
    [Fact]
    public async Task AgentUnderBudget_Returns200()
    {
        var agentId = $"budget-under-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });
        // Seed 5 000 tokens used — well under the 10 000 limit.
        await fixture.SeedBudgetUsageAsync(agentId, 5_000);

        var response = await GetAsync(agentId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AgentAtBudgetLimit_Returns429()
    {
        var agentId = $"budget-at-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });
        // Seed exactly at the default daily limit.
        await fixture.SeedBudgetUsageAsync(agentId, 100_000);

        var response = await GetAsync(agentId);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AgentOverBudgetLimit_Returns429()
    {
        var agentId = $"budget-over-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });
        // Seed well over the default daily limit.
        await fixture.SeedBudgetUsageAsync(agentId, 200_000);

        var response = await GetAsync(agentId);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AgentWithNoUsageRecord_Returns200()
    {
        var agentId = $"budget-fresh-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });
        // No usage seeded — Redis key does not exist, engine treats as 0 used.

        var response = await GetAsync(agentId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> GetAsync(string agentId)
    {
        var jwt = fixture.CreateAgentJwt(agentId);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/TestTool");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return fixture.Client.SendAsync(request);
    }
}
