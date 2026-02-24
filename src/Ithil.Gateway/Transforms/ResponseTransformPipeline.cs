using Ithil.Core.Interfaces;
using Ithil.Core.Models;

namespace Ithil.Gateway.Transforms;

/// <summary>
/// Runs the outbound response pipeline: PII scrubbing, usage recording, and trace event emission;
/// </summary>
public class ResponseTransformPipeline(
    IPrivacyFilter privacyFilter,
    IBudgetEngine budgetEngine,
    ITraceNotifier traceNotifier)
{
    /// <summary>
    /// Scrubs the response body, records token usage and fires a trace event.
    /// </summary>
    public async Task TransformAsync(string agentId, string traceId, string toolName, Stream body)
    {
        var scrubbedBody = await privacyFilter.ScrubAsync(body);
        var tokensUsed = scrubbedBody.Split(' ').Length;

        await budgetEngine.RecordUsageAsync(agentId, tokensUsed);

        await traceNotifier.NotifyAsync((new AgentTraceEvent
        {
            TraceId = traceId,
            AgentId = agentId,
            ToolName = toolName,
            Status = "success",
            TokensUsed = tokensUsed
        }));
    }
}
