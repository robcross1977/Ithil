using System.Diagnostics;
using System.Text.Json;
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
                // Client aborted the request — no point setting a status or emitting an error trace.
                if (context.RequestAborted.IsCancellationRequested) return;

                // Redis unavailable under FailClosed → 503 (Service Unavailable).
                // Any other unhandled exception → 500 (Internal Server Error).
                var statusCode = ex is RedisException ? 503 : 500;
                var reason = ex is RedisException ? "dependency unavailable" : "internal error";
                await ShortCircuitAsync(context, statusCode, reason);
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

    /// <summary>
    /// Writes a minimal JSON error body and completes the response so YARP detects
    /// <see cref="HttpResponse.HasStarted"/> and skips proxying. Setting the status code
    /// alone is not enough — YARP will still forward the request, and the upstream's
    /// response will overwrite the status set here.
    /// </summary>
    private static async Task ShortCircuitAsync(HttpContext context, int statusCode, string reason)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = JsonSerializer.SerializeToUtf8Bytes(new { error = reason });
        context.Response.ContentLength = payload.Length;
        await context.Response.Body.WriteAsync(payload, context.RequestAborted);
        await context.Response.CompleteAsync();
    }

    private async Task<Unit> RunChecksAsync(HttpContext context)
    {
        var identity = await identityService.ResolveAgentAsync(context);
        if (identity.IsNone)
        {
            await ShortCircuitAsync(context, 401, "unauthorized");
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
            await ShortCircuitAsync(context, 429, "budget exceeded");
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
            await ShortCircuitAsync(context, 403, "tool not allowed");
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
        // Fail open if the registry is unreachable: a slow/down schema endpoint should not
        // block unrelated tool calls. Genuine client cancellations are allowed to propagate.
        string[] requiredScopes;
        try
        {
            var toolEntry = (await toolRegistry.GetToolsAsync(context.RequestAborted)).Find(t =>
                string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            requiredScopes = toolEntry.Match(t => t.RequiredScopes, () => System.Array.Empty<string>());
        }
        catch (HttpRequestException) { requiredScopes = System.Array.Empty<string>(); }
        catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            // Schema fetch timed out (not a client abort) — fail open rather than blocking the call.
            requiredScopes = System.Array.Empty<string>();
        }
        if (requiredScopes.Length > 0)
        {
            var agentScopes = identity.Match(a => a.Scopes, () => LanguageExt.Seq<string>.Empty);
            var hasAllScopes = requiredScopes.All(s =>
                agentScopes.Exists(a => string.Equals(a, s, StringComparison.OrdinalIgnoreCase)));
            if (!hasAllScopes)
            {
                await ShortCircuitAsync(context, 403, "insufficient scopes");
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
