using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using static LanguageExt.Prelude;

namespace Ithil.Gateway.Transforms;

/// <summary>
/// Runs the inbound request pipeline: identity ersolution, budget check, allowlist check, and trace ID stamping.
/// </summary>
public class RequestTransformPipeline(
    IAgentIdentityService identityService,
    IBudgetEngine budgetEngine,
    IToolAllowlistService allowlistService,
    ITraceIdFactory traceIdFactory,
    ITraceNotifier traceNotifier)
{
    /// <summary>
    /// Validates the request and stamps the trace ID header if all checks pass.
    /// Short-circuits with the appropriate status code if any check fails.
    /// Unhandled exceptions set status 500 and fire an error trace event.
    /// </summary>
    public async Task TransformAsync(HttpContext context) 
    {
        var result = await TryAsync(() => RunChecksAsync(context)).Try();

        await result.Match(
            Succ: _ => Task.CompletedTask,
            Fail: async _ =>
            {
              context.Response.StatusCode = 500;
                await traceNotifier.NotifyAsync(new AgentTraceEvent
                {
                    TraceId = context.Request.Headers["X-Ithil-TraceId"].ToString(),
                    AgentId = string.Empty,
                    ToolName = context.Request.Path.Value ?? string.Empty,
                    Status = "error",
                    TokensUsed = 0
                });
            });
    }

    private async Task<Unit> RunChecksAsync(HttpContext context)
    {
        var identity = await identityService.ResolveAgentAsync(context);
        if(identity.IsNone)
        {
            context.Response.StatusCode = 401;
            return unit;
        }

        var agentId = identity.Match(a => a.AgentId, () => string.Empty);

        var isWithinBudget = await budgetEngine.IsWithinBudgetAsync(agentId);
        if(!isWithinBudget)
        {
            context.Response.StatusCode = 429;
            return unit;
        }

        var toolName = context.Request.Path.Value?.Split('/').LastOrDefault() ?? string.Empty;
        var isToolAllowed = await allowlistService.IsAllowedAsync(agentId, toolName);

        if(!isToolAllowed)
        {
            context.Response.StatusCode = 403;
            return unit;
        }

        context.Request.Headers["X-Ithil-TraceId"] = traceIdFactory.Create();
        return unit;
    }
}
