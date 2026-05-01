using System.Diagnostics;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using StackExchange.Redis;
using static LanguageExt.Prelude;

namespace Ithil.Gateway.Transforms;

/// <summary>
/// Runs the inbound request pipeline: identity resolution, budget check, allowlist check,
/// scope check, and trace ID stamping.
/// </summary>
public class RequestTransformPipeline(
    IAgentIdentityService identityService,
    IBudgetEngine budgetEngine,
    IToolAllowlistService allowlistService,
    IToolRegistry toolRegistry,
    ITraceIdFactory traceIdFactory,
    ITraceNotifier traceNotifier,
    IAuditLogger auditLogger
)
{
    /// <summary>
    /// Validates the request and stamps the trace ID header if all checks pass.
    /// Short-circuits with the appropriate status code if any check fails.
    /// Redis failures set status 503; all other unhandled exceptions set status 500.
    /// </summary>
    public async Task TransformAsync(HttpContext context)
    {
        var result = await TryAsync(() => RunChecksAsync(context)).Try();

        await result.Match(
            Succ: _ => Task.CompletedTask,
            Fail: async ex =>
            {
                // Redis unavailable under FailClosed → 503 (Service Unavailable).
                // Any other unhandled exception → 500 (Internal Server Error).
                context.Response.StatusCode = ex is RedisException ? 503 : 500;
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
            await auditLogger.WriteAsync(new AuditRecord
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                TraceId = string.Empty,
                AgentId = string.Empty,
                ToolName = context.Request.Path.Value ?? string.Empty,
                Outcome = "blocked",
                ErrorMessage = "identity resolution failed",
            });
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
            await auditLogger.WriteAsync(new AuditRecord
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                TraceId = string.Empty,
                AgentId = agentId,
                ToolName = context.Request.Path.Value ?? string.Empty,
                Outcome = "blocked",
                ErrorMessage = "budget exceeded",
            });
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
            await auditLogger.WriteAsync(new AuditRecord
            {
                Timestamp = DateTime.UtcNow.ToString("O"),
                TraceId = string.Empty,
                AgentId = agentId,
                ToolName = toolName,
                Outcome = "blocked",
                ErrorMessage = "tool not allowed",
            });
            return unit;
        }

        // Scope check — verify the agent holds every scope the tool requires.
        var toolEntry = (await toolRegistry.GetToolsAsync()).Find(t =>
            string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
        var requiredScopes = toolEntry.Match(t => t.RequiredScopes, () => []);
        if (requiredScopes.Length > 0)
        {
            var agentScopes = identity.Match(a => a.Scopes, () => LanguageExt.Seq<string>.Empty);
            var hasAllScopes = requiredScopes.All(s =>
                agentScopes.Exists(a => string.Equals(a, s, StringComparison.OrdinalIgnoreCase)));
            if (!hasAllScopes)
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
                await auditLogger.WriteAsync(new AuditRecord
                {
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    TraceId = string.Empty,
                    AgentId = agentId,
                    ToolName = toolName,
                    Outcome = "blocked",
                    ErrorMessage = "insufficient scopes",
                });
                return unit;
            }
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
