using System.Diagnostics;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
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
    ITraceNotifier traceNotifier
)
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
                await traceNotifier.NotifyAsync(
                    new AgentTraceEvent
                    {
                        TraceId = context.Request.Headers["X-Ithil-TraceId"].ToString(),
                        AgentId = string.Empty,
                        ToolName = context.Request.Path.Value ?? string.Empty,
                        Status = "error",
                        TokensUsed = 0,
                        Timestamp = DateTime.UtcNow.ToString("O"),
                    }
                );
            }
        );
    }

    private async Task<Unit> RunChecksAsync(HttpContext context)
    {
        var identity = await identityService.ResolveAgentAsync(context);
        if (identity.IsNone)
        {
            context.Response.StatusCode = 401;
            await traceNotifier.NotifyAsync(
                new AgentTraceEvent
                {
                    TraceId = string.Empty,
                    AgentId = string.Empty,
                    ToolName = context.Request.Path.Value ?? string.Empty,
                    Status = "blocked",
                    Timestamp = DateTime.UtcNow.ToString("O"),
                }
            );
            return unit;
        }

        var agentId = identity.Match(a => a.AgentId, () => string.Empty);

        var isWithinBudget = await budgetEngine.IsWithinBudgetAsync(agentId);
        if (!isWithinBudget)
        {
            context.Response.StatusCode = 429;
            await traceNotifier.NotifyAsync(
                new AgentTraceEvent
                {
                    TraceId = string.Empty,
                    AgentId = agentId,
                    ToolName = context.Request.Path.Value ?? string.Empty,
                    Status = "blocked",
                    Timestamp = DateTime.UtcNow.ToString("O"),
                }
            );
            return unit;
        }

        var toolName = context.Request.Path.Value?.Split('/').LastOrDefault() ?? string.Empty;
        var isToolAllowed = await allowlistService.IsAllowedAsync(agentId, toolName);

        if (!isToolAllowed)
        {
            context.Response.StatusCode = 403;
            await traceNotifier.NotifyAsync(
                new AgentTraceEvent
                {
                    TraceId = string.Empty,
                    AgentId = agentId,
                    ToolName = toolName,
                    Status = "blocked",
                    Timestamp = DateTime.UtcNow.ToString("O"),
                }
            );
            return unit;
        }

        // Store agentId and toolName in Items so the response transform can read them back.
        context.Items["Ithil.AgentId"] = agentId;
        context.Items["Ithil.ToolName"] = toolName;
        context.Items["Ithil.Stopwatch"] = Stopwatch.StartNew();

        var traceId = traceIdFactory.Create();
        context.Request.Headers["X-Ithil-TraceId"] = traceId;

        await traceNotifier.NotifyAsync(
            new AgentTraceEvent
            {
                TraceId = traceId,
                AgentId = agentId,
                ToolName = toolName,
                Status = "pending",
                Timestamp = DateTime.UtcNow.ToString("O"),
            }
        );
        return unit;
    }
}
