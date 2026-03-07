using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ithil.SourceGenerator;

// [Generator] tells Roslyn to load this class into the compiler pipeline at build time.
// IIncrementalGenerator is the modern generator style — it only re-runs the parts of the
// pipeline that changed, rather than scanning the whole project from scratch every build.
[Generator]
public class AgentToolGenerator : IIncrementalGenerator
{
    // DiagnosticDescriptor defines a reusable warning template.
    // ITHIL001 fires when a developer puts [AgentTool] on a non-public method by mistake.
    private static readonly DiagnosticDescriptor NonPublicMethodWarning = new(
        id: "ITHIL001",
        title: "Non-public AgentTool method",
        messageFormat: "Method '{0}' is decorated with [AgentTool] but it is not public. It will not be included in the schema registry.",
        category: "Ithil",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Initialize is called once by Roslyn to set up the generator pipeline.
    // Nothing runs here — we're just declaring what to watch for and what to do when we find it.
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ForAttributeWithMetadataName efficiently watches the syntax tree for any method
        // decorated with [AgentTool]. The predicate filters to only method declarations
        // (not classes or properties). The transform captures both the method symbol and
        // the attribute data together in an AnnotatedMethod — we bundle them here because
        // attribute constructor arguments are only reliably readable from the generator
        // context (ctx.Attributes), not from a later GetAttributes() call.
        var allAnnotatedMethods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Ithil.Attributes.AgentToolAttribute",
                predicate: (node, _) => node is MethodDeclarationSyntax,
                transform: (ctx, _) => {
                    if (ctx.TargetSymbol is not IMethodSymbol method) return null;
                    var attribute = ctx.Attributes.FirstOrDefault();
                    return attribute is null ? null : new AnnotatedMethod(method, attribute);
                })
            .Where(m => m != null);

        // Pipeline branch 1: fire a build warning for any non-public method with [AgentTool].
        // These methods are intentionally excluded from the schema registry.
        context.RegisterSourceOutput(
            allAnnotatedMethods
                .Where(m => m!.Method.DeclaredAccessibility != Accessibility.Public)
                .Collect(),
            (ctx, methods) =>
            {
                foreach (var m in methods)
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        NonPublicMethodWarning,
                        m!.Method.Locations.FirstOrDefault() ?? Location.None,
                        m.Method.Name));
            });

        // Pipeline branch 2: extract metadata from public methods and generate the registry.
        // .Collect() batches all found methods so we can write a single output file.
        var tools = allAnnotatedMethods
            .Where(m => m!.Method.DeclaredAccessibility == Accessibility.Public)
            .Select(ExtractToolMetadata)
            .Where(t => t != null)
            .Collect();

        context.RegisterSourceOutput(tools, GenerateSchemaRegistry);
    }

    // Extracts everything we need from one [AgentTool] method into a plain ToolMetadata object.
    // ConstructorArguments[0] is the required description string passed to [AgentTool("...")].
    // NamedArguments are the optional named parameters like AllowWrite = true.
    // A parameter is "required" in JSON Schema if it is non-nullable and has no default value.
    private static ToolMetadata? ExtractToolMetadata(AnnotatedMethod? annotated, System.Threading.CancellationToken _)
    {
        if (annotated is null) return null;

        var method = annotated.Method;
        var attribute = annotated.Attribute;

        // ConstructorArguments[0] = the first positional argument — the description string.
        var description = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty
            : string.Empty;

        var allowWrite = GetNamedBool(attribute, "AllowWrite");
        var maxTokens = GetNamedInt(attribute, "MaxResponseTokens", 2000);
        var category = GetNamedString(attribute, "Category");

        // Map each C# parameter to its JSON Schema equivalent.
        // NullableAnnotation.Annotated means the type has a ? suffix (e.g. int?).
        // HasExplicitDefaultValue means the parameter has a default (e.g. int x = 0).
        // Either of these makes the parameter optional in the JSON Schema required array.
        var parameters = method.Parameters
            .Select(p =>
            {
                var (jsonType, format) = TypeMapper.ToJsonType(p.Type);
                var isNullable = p.Type.NullableAnnotation == NullableAnnotation.Annotated
                    || p.HasExplicitDefaultValue;

                return new ParameterMetadata(p.Name, jsonType, format, !isNullable);
            })
            .ToArray();

        var (httpMethod, methodTemplate) = ReadHttpMethodAttribute(method);
        var classPrefix = ReadClassRoutePrefix(method);

        // Combine class [Route] prefix with method-level template.
        // Handles cases where either part is empty to avoid double slashes.
        var routePattern = string.IsNullOrEmpty(classPrefix)
            ? methodTemplate
            : string.IsNullOrEmpty(methodTemplate)
                ? classPrefix
                : $"{classPrefix.TrimEnd('/')}/{methodTemplate.TrimStart('/')}";

        var parameterSources = DetermineParameterSources(method, routePattern);

        return new ToolMetadata(
            method.Name,
            description,
            allowWrite,
            maxTokens,
            category,
            Array.Empty<string>(),
            parameters,
            httpMethod,
            routePattern,
            parameterSources);
    }

    // Writes the SchemaRegistry.g.cs file into the compilation.
    // This file is emitted as a string and compiled alongside the developer's own code.
    // At runtime, SchemaRegistry.Tools is just a normal static list — no reflection needed.
    // The file is re-emitted every build, but Roslyn's incremental engine only triggers
    // this method when the collected tools actually change.
    private static void GenerateSchemaRegistry(
        SourceProductionContext context,
        ImmutableArray<ToolMetadata?> tools)
    {
        var validTools = tools.Where(t => t != null).ToArray();
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace Ithil.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class SchemaRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    public static IReadOnlyList<ToolEntry> Tools { get; } = new List<ToolEntry>");
        sb.AppendLine("    {");

        foreach (var tool in validTools)
        {
            sb.AppendLine("        new ToolEntry");
            sb.AppendLine("        {");
            sb.AppendLine($"            Name = \"{tool!.MethodName}\",");
            sb.AppendLine($"            Description = \"{Escape(tool.Description)}\",");
            sb.AppendLine($"            AllowWrite = {tool.AllowWrite.ToString().ToLower()},");
            sb.AppendLine($"            MaxResponseTokens = {tool.MaxResponseTokens},");
            sb.AppendLine($"            Category = {(tool.Category == null ? "null" : $"\"{tool.Category}\"")},");
            sb.AppendLine($"            HttpMethod = \"{tool!.HttpMethod}\",");
            sb.AppendLine($"            RoutePattern = \"{Escape(tool.RoutePattern)}\",");
            sb.AppendLine("            ParameterSources = new Dictionary<string, string>");
            sb.AppendLine("            {");
            foreach (var kvp in tool.ParameterSources)
                sb.AppendLine($"                {{ \"{kvp.Key}\", \"{kvp.Value}\" }},");
            sb.AppendLine("            },");
            sb.AppendLine("        },");
        }

        sb.AppendLine("    };");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public class ToolEntry");
        sb.AppendLine("{");
        sb.AppendLine("    public string Name { get; set; } = string.Empty;");
        sb.AppendLine("    public string Description { get; set; } = string.Empty;");
        sb.AppendLine("    public bool AllowWrite { get; set; }");
        sb.AppendLine("    public int MaxResponseTokens { get; set; }");
        sb.AppendLine("    public string? Category { get; set; }");
        sb.AppendLine("    public string HttpMethod { get; set; } = string.Empty;");
        sb.AppendLine("    public string RoutePattern { get; set; } = string.Empty;");
        sb.AppendLine("    public Dictionary<string, string> ParameterSources { get; set; } = new();");
        sb.AppendLine("}");

        // AddSource registers the file with Roslyn. The filename must be unique per generator.
        context.AddSource("SchemaRegistry.g.cs", sb.ToString());
    }

    // Attribute named arguments come back as KeyValuePair<string, TypedConstant>.
    // TypedConstant.Value is untyped — we pattern match to the expected type.
    private static bool GetNamedBool(AttributeData attr, string name)
    {
        var arg = attr.NamedArguments.FirstOrDefault(a => a.Key == name);
        return arg.Key != null && arg.Value.Value is bool b && b;
    }

    private static int GetNamedInt(AttributeData attr, string name, int defaultValue)
    {
        var arg = attr.NamedArguments.FirstOrDefault(a => a.Key == name);
        return arg.Key != null && arg.Value.Value is int i ? i : defaultValue;
    }

    private static string? GetNamedString(AttributeData attr, string name)
    {
        var arg = attr.NamedArguments.FirstOrDefault(a => a.Key == name);
        return arg.Key != null ? arg.Value.Value?.ToString() : null;
    }

    // Reads [HttpGet("...")], [HttpPost("...")], etc. from the method symbol.
    // Returns the HTTP verb and method-level route template (without class prefix).
    // Returns empty strings if no HTTP attribute is found.
    private static (string HttpMethod, string RouteTemplate) ReadHttpMethodAttribute(IMethodSymbol method)
    {
        var httpVerbs = new Dictionary<string, string>
        {
            ["Microsoft.AspNetCore.Mvc.HttpGetAttribute"]    = "GET",
            ["Microsoft.AspNetCore.Mvc.HttpPostAttribute"]   = "POST",
            ["Microsoft.AspNetCore.Mvc.HttpPutAttribute"]    = "PUT",
            ["Microsoft.AspNetCore.Mvc.HttpDeleteAttribute"] = "DELETE",
            ["Microsoft.AspNetCore.Mvc.HttpPatchAttribute"]  = "PATCH"
        };

        foreach (var attr in method.GetAttributes())
        {
            var fullName = attr.AttributeClass?.ToDisplayString();
            if (fullName is null || !httpVerbs.TryGetValue(fullName, out var verb)) continue;

            // ConstructorArgument[0] is the optional route template string.
            var template = attr.ConstructorArguments.Length > 0
                ? attr.ConstructorArguments[0].Value?.ToString() ?? string.Empty
                : string.Empty;

            return (verb, template);
        }

        return (string.Empty, string.Empty);
    }

    // Reads [Route("...")] from the containing class of the method.
    // Returns empty string if no Route attribute exists on the class.
    private static string ReadClassRoutePrefix(IMethodSymbol method)
    {
        var routeAttr = method.ContainingType?.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Mvc.RouteAttribute");

        return routeAttr?.ConstructorArguments.Length > 0
            ? routeAttr.ConstructorArguments[0].Value?.ToString() ?? string.Empty
            : string.Empty;
    }

    // Infers how each parameter is bound based on route template, attributes, and type complexity.
    // Priority: [FromBody] > [FromQuery] > appears in route template > single type -> query > complex -> body
    private static Dictionary<string, string> DetermineParameterSources(
        IMethodSymbol method, string routePattern)
    {
        // Extract {paramName} tokens from the combined route pattern
        var routeParamMatches = System.Text.RegularExpressions.Regex
            .Matches(routePattern, @"\{(\w+)\}");
        var routeParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match m in routeParamMatches)
            routeParams.Add(m.Groups[1].Value);

        var sources = new Dictionary<string, string>();

        foreach ( var param in method.Parameters)
        {
            // [FromBody] wins regardless of anything else
            if (param.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Mvc.FromBodyAttribute"))
            {
                sources[param.Name] = "body";
                continue;
            }

            // [FromQuery] explicitly forces query string
            if (param.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Mvc.FromQueryAttribute"))
            {
                sources[param.Name] = "query";
                continue;
            }

            // Name matches a {token} in the route template -> route param
            if (routeParams.Contains(param.Name))
            {
                sources[param.Name] = "route";
                continue;
            }

            // Simple types (string, int, bool, etc.) default to query - same as ASP.NET Core
            var isSimple = param.Type.SpecialType != SpecialType.None
                || param.Type.TypeKind == TypeKind.Enum;

            sources[param.Name] = isSimple ? "query" : "body";
        }

        return sources;
    }

    // Escapes quotes inside attribute description strings so they don't break the generated C# source.
    private static string Escape(string s) => s.Replace("\"", "\\\"");
}
