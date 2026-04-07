using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Ithil.Gateway.Transforms;

/// <summary>
/// Wraps an MCP tool call with the full governance pipeline:
/// budget pre-check, trace events, PII scrubbing, token counting,
/// usage recording, and audit logging.
/// </summary>
public class ToolCallGovernancePipeline(
    IBudgetEngine budgetEngine,
    IPrivacyFilter privacyFilter,
    ITokenCounter tokenCounter,
    ITraceIdFactory traceIdFactory,
    ITraceNotifier traceNotifier,
    IAuditLogger auditLogger)
{
    /// <summary>
    /// Checks the agent's budget, invokes the downstream call, scrubs the response,
    /// records token usage, and emits trace and audit events.
    /// Throws if the budget is exceeded or the downstream call fails.
    /// </summary>
    public async Task<string> ExecuteAsync(
        string agentId,
        string toolName,
        Func<Task<string>> invoke,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isWithinBudget = await budgetEngine.IsWithinBudgetAsync(agentId, cancellationToken);
        if (!isWithinBudget)
        {
            var deniedTraceId = traceIdFactory.Create();
            await traceNotifier.NotifyAsync(new AgentTraceEvent
            {
                TraceId = deniedTraceId, AgentId = agentId, ToolName = toolName,
                Status = "denied", TokensUsed = 0,
                Timestamp = DateTime.UtcNow.ToString("O"),
            }, cancellationToken);
            await auditLogger.WriteAsync(new AuditRecord
            {
                Timestamp = DateTime.UtcNow.ToString("O"), TraceId = deniedTraceId,
                AgentId = agentId, ToolName = toolName, Outcome = "denied",
                ErrorMessage = $"Agent '{agentId}' has exceeded its token budget.",
            }, cancellationToken);
            throw new InvalidOperationException($"Agent '{agentId}' has exceeded its token budget.");
        }

        var traceId = traceIdFactory.Create();
        var stopwatch = Stopwatch.StartNew();

        cancellationToken.ThrowIfCancellationRequested();

        await traceNotifier.NotifyAsync(new AgentTraceEvent
        {
            TraceId = traceId, AgentId = agentId, ToolName = toolName,
            Status = "pending", Timestamp = DateTime.UtcNow.ToString("O"),
        }, cancellationToken);

        var result = await TryAsync(() => RunGovernedCallAsync(agentId, toolName, traceId, stopwatch, invoke, cancellationToken)).Try();

        await result.Match(
            Succ: _ => Task.CompletedTask,
            Fail: async ex =>
            {
                var status = ex is OperationCanceledException ? "cancelled" : "error";
                await traceNotifier.NotifyAsync(new AgentTraceEvent
                {
                    TraceId = traceId, AgentId = agentId, ToolName = toolName,
                    Status = status, TokensUsed = 0,
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                }, cancellationToken);
                await auditLogger.WriteAsync(new AuditRecord
                {
                    Timestamp = DateTime.UtcNow.ToString("O"), TraceId = traceId,
                    AgentId = agentId, ToolName = toolName, Outcome = status,
                    ErrorMessage = ex.Message,
                    LatencyMs = (int?)stopwatch.ElapsedMilliseconds,
                }, cancellationToken);
            }
        );

        // Preserve original stack trace when rethrowing downstream failures.
        return result.Match(
            Succ: s => s,
            Fail: ex =>
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
                return string.Empty; // unreachable; satisfies compiler
            });
    }

    /// <summary>
    /// Records a cache-hit trace event and audit entry without consuming budget or calling downstream.
    /// </summary>
    public async Task RecordCacheHitAsync(string agentId, string toolName)
    {
        var traceId = traceIdFactory.Create();
        await traceNotifier.NotifyAsync(new AgentTraceEvent
        {
            TraceId = traceId, AgentId = agentId, ToolName = toolName,
            Status = "cache-hit", TokensUsed = 0,
            Timestamp = DateTime.UtcNow.ToString("O"),
            LatencyMs = 0,
        });
        await auditLogger.WriteAsync(new AuditRecord
        {
            Timestamp = DateTime.UtcNow.ToString("O"), TraceId = traceId,
            AgentId = agentId, ToolName = toolName, Outcome = "cache-hit",
            TokensUsed = 0, CacheHit = true,
        });
    }

    private async Task<string> RunGovernedCallAsync(
        string agentId, string toolName, string traceId, Stopwatch stopwatch, Func<Task<string>> invoke,
        CancellationToken cancellationToken)
    {
        var rawBody = await invoke();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        var scrubbed = await privacyFilter.ScrubAsync(stream, cancellationToken);
        var tokensUsed = tokenCounter.CountTokens(scrubbed);

        await budgetEngine.RecordUsageAsync(agentId, tokensUsed, cancellationToken);

        await traceNotifier.NotifyAsync(new AgentTraceEvent
        {
            TraceId = traceId, AgentId = agentId, ToolName = toolName,
            Status = "success", TokensUsed = tokensUsed,
            Timestamp = DateTime.UtcNow.ToString("O"),
            LatencyMs = stopwatch.ElapsedMilliseconds,
        }, cancellationToken);

        await auditLogger.WriteAsync(new AuditRecord
        {
            Timestamp = DateTime.UtcNow.ToString("O"), TraceId = traceId,
            AgentId = agentId, ToolName = toolName, Outcome = "success",
            TokensUsed = tokensUsed, PiiScrubbed = true,
            LatencyMs = (int?)stopwatch.ElapsedMilliseconds,
        }, cancellationToken);

        return scrubbed;
    }
}
