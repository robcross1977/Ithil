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
    ITraceNotifier traceNotifier)
{
    /// <summary>
    /// Scrubs the response body, records token usage and fires a trace event.
    /// Failures fire an error trace event instead of propagating the exception.
    /// </summary>
    public async Task TransformAsync(string agentId, string traceId, string toolName, Stream body)
    {
        var result = await TryAsync(() => RunResponsePipelineAsync(agentId, traceId, toolName, body)).Try();

        await result.Match(
            Succ: _ => Task.CompletedTask,
            Fail: async _ => await traceNotifier.NotifyAsync(new AgentTraceEvent
            {
                TraceId = traceId,
                AgentId = agentId,
                ToolName = toolName,
                Status = "error",
                TokensUsed = 0
            }));
    }

    private async Task<Unit> RunResponsePipelineAsync(string agentId, string traceId, string toolName, Stream body)
    {  
        var scrubbedBody = await privacyFilter.ScrubAsync(body);
        var tokensUsed = scrubbedBody.Split(' ').Length;

        await budgetEngine.RecordUsageAsync(agentId, tokensUsed);

        await traceNotifier.NotifyAsync(new AgentTraceEvent
        {
            TraceId = traceId,
            AgentId = agentId,
            ToolName = toolName,
            Status = "success",
            TokensUsed = tokensUsed
        });

        return unit;
    }
}
