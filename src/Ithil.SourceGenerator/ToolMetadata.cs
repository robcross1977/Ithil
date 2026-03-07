using System.Collections.Generic;
namespace Ithil.SourceGenerator;

public class ToolMetadata(
    string methodName,
    string description,
    bool allowWrite,
    int maxResponseTokens,
    string? category,
    string[] requiredScopes,
    ParameterMetadata[] parameters,
    string httpMethod,
    string routePattern,
    Dictionary<string, string> parameterSources)
{
    public string MethodName { get; } = methodName;
    public string Description { get; } = description;
    public bool AllowWrite { get; } = allowWrite;
    public int MaxResponseTokens { get; } = maxResponseTokens;
    public string? Category { get; } = category;
    public string[] RequiredScopes { get; } = requiredScopes;
    public ParameterMetadata[] Parameters { get; } = parameters;
    public string HttpMethod { get; } = httpMethod;
    public string RoutePattern { get; } = routePattern;
    public Dictionary<string, string> ParameterSources { get; } = parameterSources;
}
