using Ithil.Core.Crypto;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using Ithil.Management.Models;
using LanguageExt;
using System.Security.Cryptography;

namespace Ithil.Management.Services;

/// <summary>
/// Manages agent lifecycle — creation, retrieval, update, and deletion.
/// Generates API keys on creation and stores only the SHA-256 hash.
/// </summary>
public class AgentManagementService(
    IAgentConfigRepository agentConfigs,
    IApiKeyRepository apiKeys) : IAgentManagementService
{
    public async Task<Either<ManagementError, Seq<AgentResponse>>> GetAllAsync()
    {
        var configs = await agentConfigs.GetAllAsync();
        return Either<ManagementError, Seq<AgentResponse>>.Right(configs.Map(ToResponse));
    }

    public async Task<Either<ManagementError, AgentResponse>> GetAsync(string agentId)
    {
        var config = await agentConfigs.GetAsync(agentId);
        return config.Match<Either<ManagementError, AgentResponse>>(
            Some: c  => ToResponse(c),
            None: () => new ManagementError.NotFound(agentId));
    }

    public async Task<Either<ManagementError, CreateAgentResponse>> CreateAsync(CreateAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return new ManagementError.Invalid("Label is required.");

        if (request.DailyTokenBudget <= 0)
            return new ManagementError.Invalid("DailyTokenBudget must be greater than 0.");

        var agentId  = GenerateAgentId();
        var plainKey = await apiKeys.CreateAsync(agentId);

        var config = new AgentConfig
        {
            AgentId          = agentId,
            Label            = request.Label,
            DailyTokenBudget = request.DailyTokenBudget,
            AllowedTools     = (request.AllowedTools ?? []).ToSeq(),
            Scopes           = (request.Scopes ?? []).ToSeq(),
            ApiKeyHash       = ApiKeyHasher.Hash(plainKey),
            IsActive         = true,
        };

        await agentConfigs.UpsertAsync(config);

        return new CreateAgentResponse
        {
            AgentId          = agentId,
            ApiKey           = plainKey,
            Label            = config.Label,
            DailyTokenBudget = config.DailyTokenBudget,
            AllowedTools     = [.. config.AllowedTools],
            Scopes           = [.. config.Scopes],
            IsActive         = config.IsActive,
        };
    }

    public async Task<Either<ManagementError, AgentResponse>> UpdateAsync(
        string agentId, UpdateAgentRequest request)
    {
        if (request.Label is not null && string.IsNullOrWhiteSpace(request.Label))
            return new ManagementError.Invalid("Label cannot be blank.");

        if (request.DailyTokenBudget is not null && request.DailyTokenBudget <= 0)
            return new ManagementError.Invalid("DailyTokenBudget must be greater than 0.");

        var existing = await agentConfigs.GetAsync(agentId);
        if (existing.IsNone)
            return new ManagementError.NotFound(agentId);

        var config  = existing.Match(x => x, () => default!);
        var updated = config with
        {
            Label            = request.Label            ?? config.Label,
            DailyTokenBudget = request.DailyTokenBudget ?? config.DailyTokenBudget,
            AllowedTools     = request.AllowedTools?.ToSeq() ?? config.AllowedTools,
            Scopes           = request.Scopes?.ToSeq()       ?? config.Scopes,
            IsActive         = request.IsActive         ?? config.IsActive,
        };

        await agentConfigs.UpsertAsync(updated);
        return ToResponse(updated);
    }

    public async Task<Either<ManagementError, Unit>> DeleteAsync(string agentId)
    {
        var existing = await agentConfigs.GetAsync(agentId);
        if (existing.IsNone)
            return new ManagementError.NotFound(agentId);

        var config = existing.Match(x => x, () => default!);
        var deleted = await agentConfigs.DeleteAsync(agentId);
        if (!deleted)
            return new ManagementError.NotFound(agentId);

        if (config.ApiKeyHash is not null)
            await apiKeys.DeleteAsync(config.ApiKeyHash);
        return Unit.Default;
    }

    // Produces a stable agentId: agt_ prefix + 8 random bytes as lowercase hex.
    private static string GenerateAgentId() =>
        $"agt_{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";

    private static AgentResponse ToResponse(AgentConfig config) => new()
    {
        AgentId          = config.AgentId,
        Label            = config.Label,
        DailyTokenBudget = config.DailyTokenBudget,
        AllowedTools     = [.. config.AllowedTools],
        Scopes           = [.. config.Scopes],
        IsActive         = config.IsActive,
    };
}
