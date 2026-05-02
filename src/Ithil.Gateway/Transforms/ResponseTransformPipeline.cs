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
    /// Scrubs the response body, records token usage, and fires a trace event.
    /// Returns the scrubbed body so the caller can write it back to the response.
    /// Failures fire an error trace event and return the empty string rather than propagating.
    /// </summary>
    public async Task<string> TransformAsync(
        string agentId,
        string traceId,
        string toolName,
        Stream body,
        long? latencyMs
    )
    {
        string scrubbedBody = string.Empty;

        var result = await TryAsync(async () =>
            {
                scrubbedBody = await RunResponsePipelineAsync(agentId, traceId, toolName, body, latencyMs);
                return unit;
            })
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

        return scrubbedBody;
    }

    private async Task<string> RunResponsePipelineAsync(
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

        return scrubbedBody;
    }
}
