using Ithil.Management.Models;
using Ithil.Management.Services;
using LanguageExt;
using System.Collections.Concurrent;

namespace Ithil.Dashboard.PlaywrightTests.Fixtures;

/// <summary>
/// Thread-safe in-memory implementation of IAgentManagementService for Playwright tests.
/// Call Reset() between tests to return to a clean state.
/// </summary>
public sealed class InMemoryAgentManagementService : IAgentManagementService
{
    private readonly ConcurrentDictionary<string, AgentResponse> _agents = new();

    /// <summary>Removes all agents — call this in each test's InitializeAsync.</summary>
    public void Reset() => _agents.Clear();

    public Task<Either<ManagementError, Seq<AgentResponse>>> GetAllAsync() =>
        Task.FromResult(Either<ManagementError, Seq<AgentResponse>>.Right(
            _agents.Values.ToSeq()));

    public Task<Either<ManagementError, AgentResponse>> GetAsync(string agentId) =>
        _agents.TryGetValue(agentId, out var agent)
            ? Task.FromResult(Either<ManagementError, AgentResponse>.Right(agent))
            : Task.FromResult(Either<ManagementError, AgentResponse>.Left(
                (ManagementError)new ManagementError.NotFound(agentId)));

    public Task<Either<ManagementError, CreateAgentResponse>> CreateAsync(CreateAgentRequest request)
    {
        var agentId = Guid.NewGuid().ToString("N")[..8];
        var agent = new AgentResponse
        {
            AgentId = agentId,
            Label = request.Label,
            DailyTokenBudget = request.DailyTokenBudget,
            AllowedTools = request.AllowedTools,
            Scopes = request.Scopes,
            IsActive = true,
        };
        _agents[agentId] = agent;

        var response = new CreateAgentResponse
        {
            AgentId = agentId,
            ApiKey = $"test-key-{agentId}",
            Label = request.Label,
            DailyTokenBudget = request.DailyTokenBudget,
            AllowedTools = request.AllowedTools,
            Scopes = request.Scopes,
            IsActive = true,
        };
        return Task.FromResult(Either<ManagementError, CreateAgentResponse>.Right(response));
    }

    public Task<Either<ManagementError, AgentResponse>> UpdateAsync(string agentId, UpdateAgentRequest request)
    {
        if (!_agents.TryGetValue(agentId, out var existing))
            return Task.FromResult(Either<ManagementError, AgentResponse>.Left(
                (ManagementError)new ManagementError.NotFound(agentId)));

        var updated = existing with
        {
            Label = request.Label ?? existing.Label,
            DailyTokenBudget = request.DailyTokenBudget ?? existing.DailyTokenBudget,
            AllowedTools = request.AllowedTools ?? existing.AllowedTools,
            Scopes = request.Scopes ?? existing.Scopes,
            IsActive = request.IsActive ?? existing.IsActive,
        };
        _agents[agentId] = updated;
        return Task.FromResult(Either<ManagementError, AgentResponse>.Right(updated));
    }

    public Task<Either<ManagementError, Unit>> DeleteAsync(string agentId)
    {
        _agents.TryRemove(agentId, out _);
        return Task.FromResult(Either<ManagementError, Unit>.Right(Unit.Default));
    }
}
