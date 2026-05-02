using Ithil.Management.Services;

namespace Ithil.Dashboard.PlaywrightTests.Fixtures;

/// <summary>
/// Extends DashboardFixture with a live in-memory agent service so agent management
/// tests can create and delete agents without touching a real backend.
/// </summary>
public sealed class AgentManagementFixture : DashboardFixture
{
    /// <summary>The in-memory agent store. Call Reset() between tests.</summary>
    public InMemoryAgentManagementService AgentService { get; } = new();

    protected override IAgentManagementService CreateAgentService() => AgentService;
}
