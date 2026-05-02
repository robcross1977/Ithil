using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Gateway.Identity;

/// <summary>
/// Resolves an agent identity from an incoming request.
/// Tries JWT first, falls back to API key, then loads config and checks IsActive.
/// </summary>
internal class AgentIdentityService(
    IJwtIdentityResolver jwtResolver,
    IApiKeyIdentityResolver apiKeyResolver,
    IAgentConfigRepository configRepo) : IAgentIdentityService
{
    private readonly IJwtIdentityResolver _jwtResolver = jwtResolver;
    private readonly IApiKeyIdentityResolver _apiKeyResolver = apiKeyResolver;
    private readonly IAgentConfigRepository _configRepo = configRepo;

    /// <summary>
    /// Returns the verified AgentIdentity for the request, or
    /// None if authentication fails or the agent is inactive
    /// </summary>
    public async Task<Option<AgentIdentity>> ResolveAgentAsync(HttpContext context)
    {
        var agentId = await _jwtResolver.TryResolveAsync(context);
        if(agentId.IsNone)
            agentId = await _apiKeyResolver.TryResolveAsync(context);

        return await agentId.MatchAsync(
            Some: ResolveFromConfigAsync,
            None: () => Task.FromResult(Option<AgentIdentity>.None));
    }

    // Loads the agent config and maps it to an AgentIdentity
    // Returns None if the agent is not found or is inactive
    private async Task<Option<AgentIdentity>> ResolveFromConfigAsync(string agentId)
    {
        var config = await _configRepo.GetAsync(agentId);
        return config.Bind(c => c.IsActive
            ? Option<AgentIdentity>.Some(MapToIdentity(c))
            : Option<AgentIdentity>.None);
    }

    private static AgentIdentity MapToIdentity(AgentConfig config) => new()
    {
        AgentId = config.AgentId,
        Label = config.Label,
        DailyTokenBudget = config.DailyTokenBudget,
        AllowedTools = config.AllowedTools,
        Scopes = config.Scopes,
        IsActive = config.IsActive
    };
}

