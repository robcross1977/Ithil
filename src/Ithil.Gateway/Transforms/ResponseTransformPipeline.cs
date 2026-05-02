using Ithil.Core.Interfaces;
using Ithil.Core.Models;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Ithil.Gateway.Transforms;

/// <summary>
/// Runs the outbound response pipeline: PII scrubbing, usage recording, and trace event emission.
/// </summary>
public class ResponseTransformPipeline(
    IPrivacyFilter privacyFilter,
    IBudgetEngine budgetEngine,
    ITraceNotifier traceNotifier,
    IAuditLogger auditLogger,
    ITokenCounter tokenCounter
)
{
    /// <summary>
    /// Scrubs the response body, records token usage and fires a trace event.
    /// Failures fire an error trace event instead of propagating the exception.
    /// </summary>
    public async Task TransformAsync(
        string agentId,
        string traceId,
        string toolName,
        Stream body,
        long? latencyMs
    )
    {
        var result = await TryAsync(() =>
                RunResponsePipelineAsync(agentId, traceId, toolName, body, latencyMs)
            )
            .Try();

        await result.Match(
            Succ: _ => Task.CompletedTask,
            Fail: async _ =>
            {
                await traceNotifier.NotifyAsync(
                    new AgentTraceEvent
                    {
                        TraceId = traceId,
                        AgentId = agentId,
                        ToolName = toolName,
                        Status = "error",
                        TokensUsed = 0,
                        Timestamp = DateTime.UtcNow.ToString("O"),
                        LatencyMs = latencyMs,
                    }
                );
                await auditLogger.WriteAsync(new AuditRecord
                {
                    Timestamp = DateTime.UtcNow.ToString("O"),
                    TraceId = traceId,
                    AgentId = agentId,
                    ToolName = toolName,
                    Outcome = "error",
                    LatencyMs = (int?)latencyMs,
                });
            }
        );
    }

    private async Task<Unit> RunResponsePipelineAsync(
        string agentId,
        string traceId,
        string toolName,
        Stream body,
        long? latencyMs
    )
    {
        var scrubbedBody = await privacyFilter.ScrubAsync(body);
        var tokensUsed = tokenCounter.CountTokens(scrubbedBody);

        await budgetEngine.RecordUsageAsync(agentId, tokensUsed);

        await traceNotifier.NotifyAsync(
            new AgentTraceEvent
            {
                TraceId = traceId,
                AgentId = agentId,
                ToolName = toolName,
                Status = "success",
                TokensUsed = tokensUsed,
                Timestamp = DateTime.UtcNow.ToString("O"),
                LatencyMs = latencyMs,
            }
        );

        await auditLogger.WriteAsync(new AuditRecord
        {
            Timestamp = DateTime.UtcNow.ToString("O"),
            TraceId = traceId,
            AgentId = agentId,
            ToolName = toolName,
            Outcome = "success",
            TokensUsed = tokensUsed,
            LatencyMs = (int?)latencyMs,
            PiiScrubbed = true,
        });

        return unit;
    }
}
