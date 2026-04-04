using System.Diagnostics;
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
        var isWithinBudget = await budgetEngine.IsWithinBudgetAsync(agentId);
        if (!isWithinBudget)
            throw new InvalidOperationException($"Agent '{agentId}' has exceeded its token budget.");

        var traceId = traceIdFactory.Create();
        var stopwatch = Stopwatch.StartNew();

        await traceNotifier.NotifyAsync(new AgentTraceEvent
        {
            TraceId = traceId, AgentId = agentId, ToolName = toolName,
            Status = "pending", Timestamp = DateTime.UtcNow.ToString("O"),
        });

        var result = await TryAsync(() => RunGovernedCallAsync(agentId, toolName, traceId, stopwatch, invoke)).Try();

        await result.Match(
            Succ: _ => Task.CompletedTask,
            Fail: async ex =>
            {
                await traceNotifier.NotifyAsync(new AgentTraceEvent
                {
                    TraceId = traceId, AgentId = agentId, ToolName = toolName,
                    Status = "error", TokensUsed = 0,
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                });
                await auditLogger.WriteAsync(new AuditRecord
                {
                    Timestamp = DateTime.UtcNow.ToString("O"), TraceId = traceId,
                    AgentId = agentId, ToolName = toolName, Outcome = "error",
                    ErrorMessage = ex.Message,
                    LatencyMs = (int?)stopwatch.ElapsedMilliseconds,
                });
            }
        );

        return result.Match(
            Succ: s => s,
            Fail: ex => throw ex);
    }

    private async Task<string> RunGovernedCallAsync(
        string agentId, string toolName, string traceId, Stopwatch stopwatch, Func<Task<string>> invoke)
    {
        var rawBody = await invoke();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawBody));
        var scrubbed = await privacyFilter.ScrubAsync(stream);
        var tokensUsed = tokenCounter.CountTokens(scrubbed);

        await budgetEngine.RecordUsageAsync(agentId, tokensUsed);

        await traceNotifier.NotifyAsync(new AgentTraceEvent
        {
            TraceId = traceId, AgentId = agentId, ToolName = toolName,
            Status = "success", TokensUsed = tokensUsed,
            Timestamp = DateTime.UtcNow.ToString("O"),
            LatencyMs = stopwatch.ElapsedMilliseconds,
        });

        await auditLogger.WriteAsync(new AuditRecord
        {
            Timestamp = DateTime.UtcNow.ToString("O"), TraceId = traceId,
            AgentId = agentId, ToolName = toolName, Outcome = "success",
            TokensUsed = tokensUsed, PiiScrubbed = true,
            LatencyMs = (int?)stopwatch.ElapsedMilliseconds,
        });

        return scrubbed;
    }
}
