using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;

namespace Ithil.Gateway.Stubs;

internal class NotImplementedAgentIdentityService: IAgentIdentityService
{
    public Task<Option<AgentIdentity>> ResolveAgentAsync(HttpContext context) =>
        throw new NotImplementedException("AgentIdentityService not yet implemented");
}

internal class NotImplementedBudgetEngine : IBudgetEngine
{
    public Task<bool> IsWithinBudgetAsync(string agentId) =>
        throw new NotImplementedException("BudgetEngine not yet implemented");

    public Task RecordUsageAsync(string agentId, int tokens) =>
        throw new NotImplementedException("BudgetEngine not yet implemented");
}

internal class NotImplementedToolAllowlistService : IToolAllowlistService
{
    public Task<bool> IsAllowedAsync(string agentId, string toolName) =>
        throw new NotImplementedException("ToolAllowlistService not yet implemented");
}

internal class NotImplementedTraceNotifier : ITraceNotifier
{
    public Task NotifyAsync(AgentTraceEvent traceEvent) =>
        throw new NotImplementedException("TraceNotifier not yet implemented");
}

internal class NotImplementedPrivacyFilter : IPrivacyFilter
{
    public Task<string> ScrubAsync(Stream body) =>
        throw new NotImplementedException("PrivacyFilter not yet implemented");
}

internal class DefaultTraceIdFactory : ITraceIdFactory
{
    public string Create() => Guid.NewGuid().ToString("N");
}


