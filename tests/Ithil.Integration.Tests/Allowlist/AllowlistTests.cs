using AwesomeAssertions;
using Ithil.Core.Models;
using Ithil.Integration.Tests.Fixtures;
using LanguageExt;
using System.Net;
using System.Net.Http.Headers;

namespace Ithil.Integration.Tests.Allowlist;

/// <summary>
/// Integration tests for tool allowlist enforcement through the full gateway pipeline.
/// The tool name is controlled by the last path segment — requests go to /api/{tool}.
/// </summary>
[Collection(GatewayCollection.Name)]
public sealed class AllowlistTests(GatewayFixture fixture)
{
    [Fact]
    public async Task AllowedTool_Returns200()
    {
        var agentId = $"allow-allowed-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId,
            Label = "Test",
            DailyTokenBudget = 10_000,
            IsActive = true,
            AllowedTools = new[] { "GetInventory", "CreateOrder" }.ToSeq(),
        });

        var response = await GetAsync(agentId, tool: "GetInventory");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ToolNotInAllowlist_Returns403()
    {
        var agentId = $"allow-blocked-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId,
            Label = "Test",
            DailyTokenBudget = 10_000,
            IsActive = true,
            AllowedTools = new[] { "GetInventory" }.ToSeq(),
        });

        var response = await GetAsync(agentId, tool: "DeleteEverything");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EmptyAllowlist_AllToolsAllowed_Returns200()
    {
        var agentId = $"allow-empty-{Guid.NewGuid():N}";
        await fixture.AgentConfigRepo.UpsertAsync(new AgentConfig
        {
            AgentId = agentId,
            Label = "Test",
            DailyTokenBudget = 10_000,
            IsActive = true,
            AllowedTools = Array.Empty<string>().ToSeq(), // empty = no restriction
        });

        var response = await GetAsync(agentId, tool: "AnyTool");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> GetAsync(string agentId, string tool)
    {
        var jwt = fixture.CreateAgentJwt(agentId);
        // The pipeline extracts the tool name from the last path segment, so /api/{tool}
        // is the correct way to exercise a specific tool's allowlist check.
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/{tool}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return fixture.Client.SendAsync(request);
    }
}
