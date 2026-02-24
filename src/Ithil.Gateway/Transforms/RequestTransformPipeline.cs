using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using Microsoft.AspNetCore.Http;

namespace Ithil.Gateway.Transforms;

/// <summary>
/// Runs the inbound request pipeline: identity ersolution, budget check, allowlist check, and trace ID stamping.
/// </summary>
public class RequestTransformPipeline(
    IAgentIdentityService identityService,
    IBudgetEngine budgetEngine,
    IToolAllowlistService allowlistService,
    ITraceIdFactory traceIdFactory)
{
    /// <summary>
    /// Validates the request and stamps the trace ID header if all checks pass.
    /// Short-circuits with the appropriate status code if any check fails.
    /// </summary>
    public async Task TransformAsync(HttpContext context) 
    {
        var identity = await identityService.ResolveAgentAsync(context);
        if(identity.IsNone)
        {
            context.Response.StatusCode = 401;
            return;
        }

        var agentId = identity.Match(a => a.AgentId, () => string.Empty);

        var isWithinBudget = await budgetEngine.IsWithinBudgetAsync(agentId);
        if(!isWithinBudget)
        {
            context.Response.StatusCode = 429;
            return;
        }

        var toolName = context.Request.Path.Value?.Split('/').LastOrDefault() ?? string.Empty;
        var isToolAllowed = await allowlistService.IsAllowedAsync(agentId, toolName);
        if(!isToolAllowed)
        {
            context.Response.StatusCode = 403;
            return;
        }

        var traceId = traceIdFactory.Create();
        context.Request.Headers["X-Ithil-TraceId"] = traceId;
    }
}
