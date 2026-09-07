using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Ithil.Hosting;

/// <summary>
/// Discovers minimal-API endpoints marked with
/// <see cref="AgentToolEndpointExtensions.WithAgentTool{TBuilder}"/> and converts them
/// into <see cref="ToolEntry"/> records.
/// </summary>
/// <remarks>
/// This mirrors what the source generator does for <c>[AgentTool]</c> controller actions,
/// but reads the routing table instead of the syntax tree. Reading it at runtime is strictly
/// more faithful: route groups, prefixes and constraints are already resolved by ASP.NET Core,
/// so there is no route template to reassemble by hand.
/// </remarks>
public static class EndpointToolDiscovery
{
    /// <summary>
    /// Walks the routing table and returns a <see cref="ToolEntry"/> for every endpoint
    /// marked with <c>WithAgentTool</c>. Unmarked endpoints are ignored.
    /// </summary>
    /// <remarks>
    /// Must be called after the host has started; <see cref="EndpointDataSource"/> is empty
    /// until then. <c>MapIthilSchema</c> handles that sequencing for you.
    /// </remarks>
    public static IReadOnlyList<ToolEntry> Discover(EndpointDataSource endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Endpoint = endpoint,
                Tool = endpoint.Metadata.GetMetadata<AgentToolMetadata>()
            })
            .Where(x => x.Tool is not null)
            .Select(x => ToToolEntry(x.Endpoint, x.Tool!))
            .ToList();
    }

    private static ToolEntry ToToolEntry(RouteEndpoint endpoint, AgentToolMetadata tool)
    {
        // RawText is the fully-resolved template including any MapGroup prefixes.
        // Normalised to match the generator, which emits templates without a leading slash.
        var routePattern = (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');

        var (sources, types) = DetermineParameterSources(
            endpoint.Metadata.GetMetadata<MethodInfo>(), endpoint.RoutePattern);

        return new ToolEntry
        {
            Name = tool.Name,
            Description = tool.Description,
            AllowWrite = tool.AllowWrite,
            MaxResponseTokens = tool.MaxResponseTokens,
            Category = tool.Category,
            // ToolEntry.RequiredScopes is a mutable array on a public setter, so it gets its
            // own copy rather than a handle on the endpoint metadata.
            RequiredScopes = tool.RequiredScopes.ToArray(),
            HttpMethod = ResolveHttpMethod(endpoint, tool),
            RoutePattern = routePattern,
            ParameterSources = sources,
            ParameterTypes = types
        };
    }

    /// <summary>
    /// Determines the single verb an agent should use to call this tool.
    /// </summary>
    /// <remarks>
    /// A tool carries exactly one verb, but an ASP.NET Core endpoint need not. Rather than
    /// guess — which would silently publish a schema that calls the wrong verb, or none —
    /// anything ambiguous throws and names the fix.
    ///
    /// <para>HEAD is filtered out because the framework adds it alongside GET on its own.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the endpoint constrains zero verbs, or more than one, and the tool did not
    /// state which to use.
    /// </exception>
    private static string ResolveHttpMethod(RouteEndpoint endpoint, AgentToolMetadata tool)
    {
        var declared = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;

        var candidates = (declared ?? Array.Empty<string>())
            .Where(m => !string.Equals(m, "HEAD", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tool.HttpMethod is not null)
        {
            // An explicit verb must still be one the endpoint actually serves, otherwise the
            // schema would advertise a call that always 405s. An unconstrained endpoint
            // (verbless Map) serves every verb, so anything is valid there.
            if (candidates.Count > 0 &&
                !candidates.Contains(tool.HttpMethod, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Agent tool '{tool.Name}' on route '{endpoint.RoutePattern.RawText}' declares " +
                    $"httpMethod '{tool.HttpMethod}', but the endpoint only serves " +
                    $"{string.Join(", ", candidates)}. Use one of those, or remove httpMethod.");
            }

            return tool.HttpMethod;
        }

        if (candidates.Count == 1) return candidates[0];

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"Agent tool '{tool.Name}' on route '{endpoint.RoutePattern.RawText}' is mapped " +
                "without an HTTP method constraint, so the verb an agent should call is ambiguous. " +
                "Map it with a verb-specific method (MapGet, MapPost, ...), or pass " +
                "httpMethod: \"GET\" to WithAgentTool.");
        }

        throw new InvalidOperationException(
            $"Agent tool '{tool.Name}' on route '{endpoint.RoutePattern.RawText}' serves multiple " +
            $"HTTP methods ({string.Join(", ", candidates)}), but a tool carries exactly one. " +
            "Pass httpMethod to WithAgentTool to say which, or register a separate endpoint " +
            "and tool per verb.");
    }

    // Reflection-based counterpart to the symbol-based logic in the source generator. Kept
    // deliberately parallel to AgentToolGenerator.DetermineParameterSources so both discovery
    // paths produce identical schemas for equivalent signatures.
    private static (Dictionary<string, string> Sources, Dictionary<string, string> Types)
        DetermineParameterSources(MethodInfo? handler, RoutePattern routePattern)
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (handler is null) return (sources, types);

        var routeParams = ExtractRouteParams(routePattern);

        var entries = handler.GetParameters()
            .SelectMany(p => ToParamEntries(p, routeParams))
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var group in entries)
        {
            var winner = HighestPriorityEntry(group);
            sources[group.Key] = winner.Source;
            types[group.Key] = winner.JsonType;
        }

        return (sources, types);
    }

    private static IEnumerable<(string Name, string Source, string JsonType)> ToParamEntries(
        ParameterInfo param, HashSet<string> routeParams)
    {
        if (param.Name is null || IsInfrastructureParam(param))
            return Array.Empty<(string, string, string)>();

        var source = ResolveSource(param, routeParams);

        if (source == "body" && IsComplexType(param.ParameterType))
        {
            var expanded = ExpandBodyType(param.ParameterType).ToList();
            if (expanded.Count > 0) return expanded;
            // Match the generator: an empty body type is still emitted as an object so the
            // router keeps sending a body rather than silently dropping the parameter.
            return new[] { (param.Name, "body", "object") };
        }

        return new[] { (param.Name, source, ToJsonType(param.ParameterType)) };
    }

    private static IEnumerable<(string Name, string Source, string JsonType)> ExpandBodyType(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (ToCamelCase(p.Name), "body", ToJsonType(p.PropertyType)));

    // Binding sources are matched through the framework metadata interfaces rather than by
    // attribute type name, so both the MVC attributes (Microsoft.AspNetCore.Mvc.FromQuery)
    // and the minimal-API ones (Microsoft.AspNetCore.Http.FromQuery) are recognised.
    private static string ResolveSource(ParameterInfo param, HashSet<string> routeParams)
    {
        var attrs = param.GetCustomAttributes(inherit: true);

        if (attrs.OfType<IFromBodyMetadata>().Any()) return "body";
        if (attrs.OfType<IFromQueryMetadata>().Any()) return "query";
        if (attrs.OfType<IFromRouteMetadata>().Any()) return "route";

        if (param.Name is not null && routeParams.Contains(param.Name)) return "route";

        return IsComplexType(param.ParameterType) ? "body" : "query";
    }

    // ASP.NET Core has already parsed the template, so its parameter list is authoritative.
    // Hand-rolling a regex over RawText gets catch-all ({*path}, {**path}), constrained and
    // optional segments wrong; RoutePattern.Parameters handles every form the router accepts.
    private static HashSet<string> ExtractRouteParams(RoutePattern routePattern) =>
        new(
            routePattern.Parameters.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

    // Parameters the framework supplies itself, which must never reach an agent as inputs.
    // Service- and header-bound parameters are excluded for the same reason: they are host
    // concerns, not something an agent can or should provide.
    private static bool IsInfrastructureParam(ParameterInfo param)
    {
        var attrs = param.GetCustomAttributes(inherit: true);
        if (attrs.OfType<IFromServiceMetadata>().Any()) return true;
        if (attrs.OfType<IFromHeaderMetadata>().Any()) return true;

        var type = param.ParameterType;
        return type == typeof(CancellationToken)
            || type == typeof(HttpContext)
            || type == typeof(HttpRequest)
            || type == typeof(HttpResponse)
            || type == typeof(System.Security.Claims.ClaimsPrincipal)
            || typeof(System.IO.Stream).IsAssignableFrom(type)
            || typeof(System.IO.Pipelines.PipeReader).IsAssignableFrom(type);
    }

    private static bool IsComplexType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t.IsPrimitive || t.IsEnum) return false;

        return t != typeof(string)
            && t != typeof(decimal)
            && t != typeof(Guid)
            && t != typeof(DateTime)
            && t != typeof(DateTimeOffset)
            && t != typeof(TimeSpan)
            && t != typeof(DateOnly)
            && t != typeof(TimeOnly);
    }

    // Mirrors Ithil.SourceGenerator.TypeMapper so both paths emit the same JSON Schema types.
    private static string ToJsonType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte))
            return "integer";
        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            return "number";
        if (t == typeof(bool))
            return "boolean";

        return "string";
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    // route > query > body, so a route or query parameter is never shadowed by a
    // same-named property expanded out of the body type.
    private static (string Name, string Source, string JsonType) HighestPriorityEntry(
        IEnumerable<(string Name, string Source, string JsonType)> entries)
    {
        static int Priority(string source) => source switch
        {
            "route" => 3,
            "query" => 2,
            _ => 1
        };
        return entries.OrderByDescending(e => Priority(e.Source)).First();
    }
}
