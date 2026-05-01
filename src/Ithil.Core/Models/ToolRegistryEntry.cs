using System.Collections.Generic;

namespace Ithil.Core.Models;

/// <summary>
/// Represents a single tool exposed by the downstream service, including routing info used by the gateway.
/// Routing fields (HttpMethod, RoutePattern, ParmeterSources) are internal to the gateway and never sent to the AI.
/// </summary>
public record ToolRegistryEntry(
    string Name,
    string Description,
    bool AllowWrite,
    int MaxResponseTokens,
    string? Category,
    string[] RequiredScopes,
    string HttpMethod,
    string RoutePattern,
    Dictionary<string, string> ParameterSources,
    McpInputSchema InputSchema);
