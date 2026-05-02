using Ithil.Management.Models;
using LanguageExt;

namespace Ithil.Management.Services;

/// <summary>
/// Creates, reads, updates, and deletes agent configurations.
/// </summary>
public interface IAgentManagementService
{
    Task<Either<ManagementError, Seq<AgentResponse>>> GetAllAsync();
    Task<Either<ManagementError, AgentResponse>> GetAsync(string agentId);
    Task<Either<ManagementError, CreateAgentResponse>> CreateAsync(CreateAgentRequest request);
    Task<Either<ManagementError, AgentResponse>> UpdateAsync(string agentId, UpdateAgentRequest request);
    Task<Either<ManagementError, Unit>> DeleteAsync(string agentId);
}
