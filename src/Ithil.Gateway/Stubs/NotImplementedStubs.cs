using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Gateway.Stubs;

internal class NotImplementedApiKeyRepository : IApiKeyRepository
{
    public Task<Option<string>> FindByHashedKeyAsync(string hashedKey) =>
        throw new NotImplementedException("ApiKeyRepository not yet implemented");

    public Task<string> CreateAsync(string agentId) =>
        throw new NotImplementedException("ApiKeyRepository not yet implemented");
}


internal class NotImplementedAgentIdentityService: IAgentIdentityService
{
    public Task<Option<AgentIdentity>> ResolveAgentAsync(HttpContext context) =>
        throw new NotImplementedException("AgentIdentityService not yet implemented");
}

internal class NotImplementedBudgetEngine : IBudgetEngine
{
    public Task<bool> IsWithinBudgetAsync(string agentId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("BudgetEngine not yet implemented");

    public Task RecordUsageAsync(string agentId, int tokens, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("BudgetEngine not yet implemented");

    public Task<int> GetUsageAsync(string agentId) =>
        throw new NotImplementedException("BudgetEngine not yet implemented");
}

// No-op until TraceNotifier is implemented.
internal class NotImplementedTraceNotifier : ITraceNotifier
{
    public Task NotifyAsync(AgentTraceEvent traceEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

// Pass-through until PrivacyFilter is implemented — returns body unchanged.
internal class NotImplementedPrivacyFilter : IPrivacyFilter
{
    public Task<string> ScrubAsync(Stream body, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(body);
        return reader.ReadToEndAsync(cancellationToken);
    }
}

internal class DefaultTraceIdFactory : ITraceIdFactory
{
    public string Create() => Guid.NewGuid().ToString("N");
}


