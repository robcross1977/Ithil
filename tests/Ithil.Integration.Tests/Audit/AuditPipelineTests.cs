using FluentAssertions;
using Ithil.Core.Models;
using Ithil.Integration.Tests.Fixtures;
using LanguageExt;
using System.Net.Http.Headers;

namespace Ithil.Integration.Tests.Audit;

/// <summary>
/// Verifies that the request governance pipeline writes audit records with the correct
/// fields when requests are blocked. The audit logger enqueues to a channel that the
/// background worker drains — tests use WaitForAuditRecordAsync to poll for the record.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class AuditPipelineTests(GatewayFixture fixture)
{
    [Fact]
    public async Task BlockedRequest_NoAuth_WritesAuditRecord_WithOutcomeBlocked()
    {
        // No Authorization header — identity resolution returns None → 401.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/TestTool");
        await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);

        var record = await fixture.WaitForAuditRecordAsync(
            r => r.ErrorMessage == "identity resolution failed");

        record.Outcome.Should().Be("blocked");
        record.AgentId.Should().BeEmpty();
        record.ToolName.Should().Contain("TestTool");
    }

    [Fact]
    public async Task BlockedRequest_BudgetExceeded_WritesAuditRecord_WithAgentId()
    {
        var agentId = $"audit-budget-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
        });
        await fixture.SeedBudgetUsageAsync(agentId, 200_000);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/TestTool");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.CreateAgentJwt(agentId));
        await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);

        var record = await fixture.WaitForAuditRecordAsync(
            r => r.AgentId == agentId && r.ErrorMessage == "budget exceeded");

        record.Outcome.Should().Be("blocked");
        record.ToolName.Should().Contain("TestTool");
    }

    [Fact]
    public async Task BlockedRequest_ToolNotAllowed_WritesAuditRecord_WithToolName()
    {
        var agentId = $"audit-allowlist-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId, Label = "Test", DailyTokenBudget = 10_000, IsActive = true,
            AllowedTools = new[] { "GetInventory" }.ToSeq(),
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/ForbiddenTool");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.CreateAgentJwt(agentId));
        await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);

        var record = await fixture.WaitForAuditRecordAsync(
            r => r.AgentId == agentId && r.ErrorMessage == "tool not allowed");

        record.Outcome.Should().Be("blocked");
        record.ToolName.Should().Be("ForbiddenTool");
    }
}
