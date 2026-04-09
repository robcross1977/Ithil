using Ithil.Core.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ithil.Gateway.Mcp;

/// <summary>
/// Builds HTTP requests from a tool registry entry and the arguments supplied by the AI agent.
/// </summary>
public static partial class ToolCallRouter
{
    /// <summary>
    /// Builds an HttpRequestMessage for the given tool and arguments.
    /// Route params are substituted into the URL path, query params appended to the query string,
    /// and body params serialized as JSON in the request body.
    /// </summary>
    public static HttpRequestMessage BuildRequest(
        ToolRegistryEntry tool,
        string baseUrl,
        JsonElement arguments)
    {
        if (string.IsNullOrEmpty(tool.HttpMethod))
            throw new InvalidOperationException(
                $"Tool '{tool.Name}' has no HTTP method. Decorate its controller method with [HttpGet], [HttpPost], etc.");

        var path = SubstituteRouteParams(tool.RoutePattern, tool.ParameterSources, arguments);
        var url = BuildFullUrl(path, tool.ParameterSources, arguments, baseUrl);
        var request = new HttpRequestMessage(new HttpMethod(tool.HttpMethod), url);

        var bodyParam = tool.ParameterSources.FirstOrDefault(p => p.Value == "body");
        if (bodyParam.Key != null && arguments.TryGetProperty(bodyParam.Key, out var bodyValue))
        {
            request.Content = new StringContent(
                bodyValue.GetRawText(),
                Encoding.UTF8,
                "application/json");
        }

        return request;
    }

    /// <summary>
    /// Extracts {paramName} tokens from a route pattern string.
    /// </summary>
    public static IEnumerable<string> RouteParamNames(string routePattern) =>
        RouteRegex().Matches(routePattern)
             .Select(m => m.Groups[1].Value);

    // Substitutes {param} tokens in the route pattern with URL-encoded argument values.
    private static string SubstituteRouteParams(
        string routePattern,
        Dictionary<string, string> parameterSources,
        JsonElement arguments)
    {
        var path = routePattern;
        foreach (var kvp in parameterSources.Where(p => p.Value == "route"))
        {
            if (arguments.TryGetProperty(kvp.Key, out var val))
                path = path.Replace($"{{{kvp.Key}}}", Uri.EscapeDataString(val.ToString()));
        }
        return path;
    }

    // Combines the base URL, path, and query string into a full request URL.
    private static string BuildFullUrl(
        string path,
        Dictionary<string, string> parameterSources,
        JsonElement arguments,
        string baseUrl)
    {
        var queryParts = parameterSources
            .Where(p => p.Value == "query" && arguments.TryGetProperty(p.Key, out _))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(arguments.GetProperty(p.Key).ToString())}");

        var query = string.Join("&", queryParts);
        var fullPath = string.IsNullOrEmpty(query) ? path : $"{path}?{query}";
        return $"{baseUrl.TrimEnd('/')}/{fullPath.TrimStart('/')}";
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex RouteRegex();
}
