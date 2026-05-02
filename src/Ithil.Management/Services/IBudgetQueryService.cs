using Ithil.Management.Models;
using LanguageExt;

namespace Ithil.Management.Services;

/// <summary>
/// Queries and resets per-agent token budget usage.
/// </summary>
public interface IBudgetQueryService
{
    Task<Either<ManagementError, BudgetStatusResponse>> GetStatusAsync(string agentId);
    Task<Either<ManagementError, Unit>> ResetAsync(string agentId, string? operatorId);
}
