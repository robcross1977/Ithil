using Ithil.Management.Models;
using Ithil.Management.Services;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Ithil.Dashboard.Services;

/// <summary>
/// Provides agent CRUD operations for the dashboard, delegating to IAgentManagementService.
/// Named AgentDashboardService to avoid collision with IAgentManagementService.
/// </summary>
public class AgentDashboardService(IAgentManagementService agentManagementService)
{
    /// <summary>
    /// Returns all registered agents.
    /// </summary>
    public Task<Either<ManagementError, Seq<AgentResponse>>> GetAllAsync() =>
        agentManagementService.GetAllAsync();

    /// <summary>
    /// Creates a new agent. Returns the response including the one-time API key on success.
    /// </summary>
    public Task<Either<ManagementError, CreateAgentResponse>> CreateAsync(CreateAgentRequest request) =>
        agentManagementService.CreateAsync(request);

    /// <summary>
    /// Deletes an agent. Returns None when the agent was successfully deleted or did not
    /// exist. Returns Some(error) only for unexpected failures.
    /// </summary>
    public async Task<Option<ManagementError>> DeleteAsync(string agentId)
    {
        var result = await agentManagementService.DeleteAsync(agentId);
        return result.Match(
            Right: _ => None,
            Left: error => error is ManagementError.NotFound ? None : Some(error)
        );
    }
}
