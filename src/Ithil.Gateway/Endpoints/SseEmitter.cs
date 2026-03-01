namespace Ithil.Gateway.Endpoints;

/// <summary>
/// Holds an SSE connection open and writes events as they arrive from the trace notifier.
/// </summary>
public class SseEmitter
{
    /// <summary>
    ///  Sets the required SSE headers and keeps the connection open until the client disconnects;
    /// </summary>
    public async Task StreamAsync(HttpContext context)
    {
        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");

        await Task.Delay(Timeout.Infinite, context.RequestAborted);
    }
}
