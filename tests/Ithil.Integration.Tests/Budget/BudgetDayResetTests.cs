using AwesomeAssertions;
using Ithil.Budget;
using Ithil.Core.Models;
using Ithil.Integration.Tests.Fixtures;
using System.Net;
using System.Net.Http.Headers;

namespace Ithil.Integration.Tests.Budget;

/// <summary>
/// Verifies that token usage is isolated per UTC calendar day.
/// Seeding yesterday's Redis key must not block today's requests,
/// proving that BudgetKeyFactory.ForDate produces distinct keys per day.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class BudgetDayResetTests(GatewayFixture fixture)
{
    [Fact]
    public async Task YesterdayUsage_DoesNotAffectToday_Returns200()
    {
        var agentId = $"budget-yesterday-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });

        // Seed yesterday's key well over the limit — today's key is absent.
        var db = fixture.Redis.GetDatabase();
        var yesterdayKey = BudgetKeyFactory.ForDate(agentId, DateTime.UtcNow.AddDays(-1));
        await db.StringSetAsync(yesterdayKey, 200_000);
        await db.KeyExpireAsync(yesterdayKey, TimeSpan.FromDays(1));

        var response = await GetAsync(agentId);

        // Today's key doesn't exist → budget engine sees 0 used → request passes.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> GetAsync(string agentId)
    {
        var jwt = fixture.CreateAgentJwt(agentId);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/TestTool");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
