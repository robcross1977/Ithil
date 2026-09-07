using AwesomeAssertions;
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
        // DailyTokenBudget is stored on AgentConfig but the engine currently enforces the
        // global DefaultDailyTokenLimit (100 000). Seeding 5 000 is well under that ceiling.
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 100_000, IsActive = true,
        });
        await fixture.SeedBudgetUsageAsync(agentId, 5_000);

        var response = await GetAsync(agentId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AgentAtBudgetLimit_Returns429()
    {
        var agentId = $"budget-at-{Guid.NewGuid():N}";
        // The engine enforces DefaultDailyTokenLimit (100 000). Seeding exactly that value
        // puts usage at the boundary — not-strictly-less-than — so the agent is blocked.
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 100_000, IsActive = true,
        });
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
            AgentId = agentId, Label = "Test", DailyTokenBudget = 100_000, IsActive = true,
        });
        // Seed well over the global daily limit.
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
            AgentId = agentId, Label = "Test", DailyTokenBudget = 100_000, IsActive = true,
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
