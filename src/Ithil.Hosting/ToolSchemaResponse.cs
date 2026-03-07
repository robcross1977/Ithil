using System.Collections.Generic;
using Ithil.Core.Models;

namespace Ithil.Hosting;

/// <summary>
/// The JSON shape returned from GET /ithil/schema.
/// Consumed by the gateway to build its internal tool registry
/// </summar>
public record ToolSchemaResponse(
    string Name,
    string Description,
    bool AllowWrite,
    int MaxResponseTokens,
    string? Category,
    string HttpMethod,
    string RoutePattern,
    Dictionary<string, string> ParameterSources,
    McpInputSchema InputSchema);
