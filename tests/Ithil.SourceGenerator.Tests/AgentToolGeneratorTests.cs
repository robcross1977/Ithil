using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ithil.SourceGenerator.Tests;

public class AgentToolGeneratorTests
{
    // Source generator tests cannot use the real Ithil.Attributes.dll directly because
    // Roslyn needs to resolve the attribute type from within the in-memory test compilation,
    // and resolving it from an external assembly requires loading all its transitive
    // dependencies (Ithil.Core, LanguageExt, etc.) which is fragile and error-prone.
    //
    // Instead we define AgentToolAttribute inline as a source string and include it in the
    // test compilation alongside the code under test. ForAttributeWithMetadataName matches
    // by fully qualified name ("Ithil.Attributes.AgentToolAttribute"), so as long as the
    // namespace and class name match the real attribute, the generator behaves identically.
    private const string AttributeSource = """
        namespace Ithil.Attributes
        {
            [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
            public sealed class AgentToolAttribute : System.Attribute
            {
                public AgentToolAttribute(string description) { Description = description; }
                public string Description { get; }
                public bool AllowWrite { get; set; }
                public int MaxResponseTokens { get; set; } = 2000;
                public string? Category { get; set; }
                public string[]? RequiredScopes { get; set; }
            }
        }
        """;

    private const string HttpAttributeSource = """
        namespace Microsoft.AspNetCore.Mvc
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public sealed class RouteAttribute : System.Attribute
            {
                public RouteAttribute(string template) { Template = template; }
                public string Template { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class HttpGetAttribute : System.Attribute
            {
                public HttpGetAttribute() {}
                public HttpGetAttribute(string template) { Template = template; }
                public string? Template { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class HttpPostAttribute : System.Attribute
            {
                public HttpPostAttribute() {}
                public HttpPostAttribute(string template) { Template = template; }
                public string? Template { get; }
            }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class FromBodyAttribute : System.Attribute {}

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class FromQueryAttribute : System.Attribute {}

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class FromRouteAttribute : System.Attribute {}
        }
    """;

    // Builds an in-memory Roslyn compilation from the given source, runs AgentToolGenerator
    // against it, and returns the output compilation, any diagnostics emitted by the generator,
    // and the full text of the generated SchemaRegistry.g.cs file.
    //
    // The compilation contains two syntax trees:
    //   1. AttributeSource — the inline AgentToolAttribute definition
    //   2. source          — the test-specific code with [AgentTool] decorated methods
    //
    // Only typeof(object) is needed as a metadata reference because all types used in the
    // test source are either from the BCL (System.Object, void, int, string) or defined
    // inline in AttributeSource. No NuGet packages or project references are required.
    private static (
        Compilation Output,
        IReadOnlyList<Diagnostic> Diagnostics,
        string GeneratedSource
    ) RunGenerator(string source)
    {
        var inputCompilation = CSharpCompilation.Create(
            "TestAssembly",
            [
                CSharpSyntaxTree.ParseText(AttributeSource),
                CSharpSyntaxTree.ParseText(HttpAttributeSource),
                CSharpSyntaxTree.ParseText(source)

            ],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new AgentToolGenerator();

        // CSharpGeneratorDriver runs the generator against the compilation.
        // RunGeneratorsAndUpdateCompilation returns the new compilation (with generated files
        // added), the generator diagnostics, and updates the driver with run results.
        // GetRunResult().GeneratedTrees contains one entry per AddSource() call in the generator.
        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGeneratorsAndUpdateCompilation(
                inputCompilation,
                out var outputCompilation,
                out var diagnostics);

        var generatedSource = driver.GetRunResult().GeneratedTrees
            .FirstOrDefault()?.GetText().ToString() ?? string.Empty;

        return (outputCompilation, diagnostics, generatedSource);
    }

    [Fact]
    public void NoAgentToolMethods_EmitsEmptySchemaRegistry()
    {
        var code = """
            public class MyController
            {
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().NotBeNullOrWhiteSpace();
        source.Should().Contain("SchemaRegistry");
        source.Should().NotContain("new ToolEntry");
    }

    [Fact]
    public void SingleMethod_EmitsCorrectName()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("GetInventory");
    }

    [Fact]
    public void SingleMethod_EmitsDescription()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("Returns stock levels for a SKU")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("Returns stock levels for a SKU");
    }

    [Fact]
    public void MultipleTools_AllEmitted()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc1")]
                public void GetInventory() {}
                [AgentTool("desc2")]
                public void GetOrders() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("GetInventory");
        source.Should().Contain("GetOrders");
    }

    [Fact]
    public void NonPublicMethod_EmitsDiagnosticWarning()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                private void HiddenTool() {}
            }
            """;

        var (_, diagnostics, _) = RunGenerator(code);
        diagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Warning && d.Id == "ITHIL001");
    }

    [Fact]
    public void SingleTool_EmitsSchemaRegistryClass()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("SchemaRegistry");
    }

    [Fact]
    public void SingleTool_EmitsToolEntryClass()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("ToolEntry");
    }

    [Fact]
    public void SingleTool_EmitsNameInEntry()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("Name = \"GetInventory\"");
    }

    [Fact]
    public void NoHttpVerbAttribute_EmitsEmptyHttpMethod()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("HttpMethod = \"\"");
    }

    [Fact]
    public void HttpGetAttribute_EmitsGetVerb()
    {
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            public class MyController {
                [AgentTool("desc")]
                [HttpGet("items")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("HttpMethod = \"GET\"");
    }

    [Fact]
    public void ParameterSources_RouteQueryAndBody_InferredCorrectly()
    {
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            [Route("api")]
            public class MyController {
                [AgentTool("desc")]
                [HttpGet("items/{sku}")]
                public void GetInventory(string sku, string filter, MyBody payload) {}
            }
            public class MyBody {}
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("{ \"sku\", \"route\" }");
        source.Should().Contain("{ \"filter\", \"query\" }");
        source.Should().Contain("{ \"payload\", \"body\" }");
    }

    [Fact]
    public void SingleTool_EmitsRoutePatternInEntry()
    {
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            [Route("api/inventory")]
            public class MyController {
                [AgentTool("desc")]
                [HttpGet("stock/{sku}")]
                public void GetInventory(string sku) {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("api/inventory/stock/{sku}");
        source.Should().Contain("HttpMethod = \"GET\"");
        source.Should().Contain("{ \"sku\", \"route\" }");
    }

    [Fact]
    public void MultipleTools_EmitMultipleEntries()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc1")]
                public void GetInventory() {}
                [AgentTool("desc2")]
                public void CreateOrder() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("Name = \"GetInventory\"");
        source.Should().Contain("Name = \"CreateOrder\"");
    }

    [Fact]
    public void GeneratedOutput_DoesNotContainMcpProxyClasses()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().NotContain("[McpServerToolType]");
        source.Should().NotContain("ExecuteAsync");
    }

    [Fact]
    public void HttpGetAttribute_EmitsGetHttpMethodValue()
    {
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            public class MyController {
                [AgentTool("desc")]
                [HttpGet]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("HttpMethod = \"GET\"");
    }

    [Fact]
    public void HttpPostAttribute_EmitsPostHttpMethodValue()
    {
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            public class MyController {
                [AgentTool("desc")]
                [HttpPost]
                public void CreateOrder() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("HttpMethod = \"POST\"");
    }

    [Fact]
    public void RouteTemplateParameter_EmittedAsRouteSource_InParameterSources()
    {
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            [Route("api/inventory")]
            public class MyController {
                [AgentTool("desc")]
                [HttpGet("stock/{sku}")]
                public void GetInventory(string sku) {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        // sku appears in {sku} in the route template — generator classifies it as "route".
        source.Should().Contain("{ \"sku\", \"route\" }");
    }

    [Fact]
    public void AllowWrite_DefaultsFalse_WhenNotSpecified()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("AllowWrite = false");
    }

    [Fact]
    public void AllowWrite_EmitsTrue_WhenExplicitlySet()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc", AllowWrite = true)]
                public void CreateOrder() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("AllowWrite = true");
    }

    [Fact]
    public void RouteConstraint_ClassifiesParamAsRoute()
    {
        // {id:int} — the constraint suffix must not prevent route-param detection.
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            [Route("api/posts")]
            public class MyController {
                [AgentTool("desc")]
                [HttpGet("{id:int}")]
                public void GetPost(int id) {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("{ \"id\", \"route\" }");
    }

    [Fact]
    public void FromBodyComplexType_ExpandsToIndividualCamelCaseProperties()
    {
        // [FromBody] with a complex type should expand into one entry per public property,
        // camelCased, all with source "body" — not a single opaque "request" entry.
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            [Route("api/posts")]
            public class MyController {
                [AgentTool("desc")]
                [HttpPost]
                public void CreatePost([FromBody] CreatePostRequest request) {}
            }
            public class CreatePostRequest {
                public string Title { get; set; } = "";
                public int UserId { get; set; }
            }
            """;

        var (_, _, source) = RunGenerator(code);

        // Individual properties appear as body entries — not the parameter name "request".
        source.Should().Contain("{ \"title\", \"body\" }");
        source.Should().Contain("{ \"userId\", \"body\" }");
        source.Should().NotContain("{ \"request\", \"body\" }");
    }

    [Fact]
    public void FromBodyComplexType_EmitsIntegerTypeForIntProperty()
    {
        // ParameterTypes for an int property must emit "integer", not "string".
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            public class MyController {
                [AgentTool("desc")]
                [HttpPost]
                public void CreatePost([FromBody] CreatePostRequest request) {}
            }
            public class CreatePostRequest {
                public int UserId { get; set; }
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("{ \"userId\", \"integer\" }");
    }

    [Fact]
    public void FromRouteAttribute_ClassifiesParamAsRoute()
    {
        // Explicit [FromRoute] attribute on a parameter that is NOT in the route template
        // must still be classified as route source.
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            [Route("api/items")]
            public class MyController {
                [AgentTool("desc")]
                [HttpGet]
                public void GetItem([FromRoute] int id) {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("{ \"id\", \"route\" }");
    }

    [Fact]
    public void CancellationToken_IsExcludedFromParameterSources()
    {
        // CancellationToken is injected by the framework and must never appear in the schema.
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            [Route("api/items")]
            public class MyController {
                [AgentTool("desc")]
                [HttpGet("{id}")]
                public void GetItem(int id, System.Threading.CancellationToken ct) {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("{ \"id\", \"route\" }");
        source.Should().NotContain("\"ct\"");
    }

    [Fact]
    public void FromBodyEmptyType_FallsBackToObjectEntry()
    {
        // A [FromBody] type with no public properties must emit a single "object"-typed entry
        // for the parameter name rather than being silently dropped.
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            public class MyController {
                [AgentTool("desc")]
                [HttpPost]
                public void CreateThing([FromBody] EmptyBody payload) {}
            }
            public class EmptyBody {}
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("{ \"payload\", \"body\" }");
        source.Should().Contain("{ \"payload\", \"object\" }");
    }

    [Fact]
    public void EmptyDescription_EmitsITHIL002Warning()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("")]
                public void GetInventory() {}
            }
            """;

        var (_, diagnostics, _) = RunGenerator(code);

        diagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Warning && d.Id == "ITHIL002");
    }

    [Fact]
    public void WhitespaceDescription_EmitsITHIL002Warning()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("   ")]
                public void GetInventory() {}
            }
            """;

        var (_, diagnostics, _) = RunGenerator(code);

        diagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Warning && d.Id == "ITHIL002");
    }

    [Fact]
    public void EmptyDescription_ExcludedFromSchemaRegistry()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().NotContain("new ToolEntry");
    }

    [Fact]
    public void MaxResponseTokens_EmittedCorrectly_WhenSpecified()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc", MaxResponseTokens = 500)]
                public void GetSummary() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("MaxResponseTokens = 500");
    }

    [Fact]
    public void Category_EmittedCorrectly_WhenSpecified()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc", Category = "Orders")]
                public void GetOrder() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("Category = \"Orders\"");
    }

    [Fact]
    public void RequiredScopes_EmittedAsArray_WhenSpecified()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc", RequiredScopes = new[] { "orders:read", "inventory:read" })]
                public void GetOrder() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("\"orders:read\"");
        source.Should().Contain("\"inventory:read\"");
    }

    [Fact]
    public void RequiredScopes_EmitsEmptyArray_WhenNotSpecified()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetOrder() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("global::System.Array.Empty<string>()");
    }

    [Fact]
    public void DescriptionWithQuotes_IsEscapedInGeneratedCode()
    {
        // Quotes in a description must be escaped so the generated C# compiles.
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("Returns the \"best\" match")]
                public void GetBest() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        // The escaped form must appear, not a raw unescaped quote that would break the source.
        source.Should().Contain("Returns the \\\"best\\\" match");
    }

    [Fact]
    public void BoolParameter_EmitsBooleanType()
    {
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            public class MyController {
                [AgentTool("desc")]
                [HttpGet]
                public void SetFlag(bool enabled) {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("{ \"enabled\", \"boolean\" }");
    }

    [Fact]
    public void DecimalParameter_EmitsNumberType()
    {
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            public class MyController {
                [AgentTool("desc")]
                [HttpPost]
                public void SetPrice(decimal amount) {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("{ \"amount\", \"number\" }");
    }

    [Fact]
    public void DateTimeParameter_EmitsStringType()
    {
        // TypeMapper maps DateTime to JSON Schema type "string" (format "date" is not stored
        // in the generated ParameterTypes dictionary, only the type string is).
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            public class MyController {
                [AgentTool("desc")]
                [HttpGet]
                public void GetByDate(System.DateTime date) {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("{ \"date\", \"string\" }");
    }

    [Fact]
    public void DuplicateName_RouteParamBeatsExpandedBodyProperty()
    {
        // When a route param name collides with a body-type property name, the route
        // classification wins (route params are inserted into entries before body expansion).
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            [Route("api/users")]
            public class MyController {
                [AgentTool("desc")]
                [HttpPost("{userId}")]
                public void CreateForUser(int userId, [FromBody] UserPayload payload) {}
            }
            public class UserPayload {
                public int UserId { get; set; }
            }
            """;

        var (_, _, source) = RunGenerator(code);

        // userId must be classified as "route", not "body" (route param appears first).
        source.Should().Contain("{ \"userId\", \"route\" }");
        source.Should().NotContain("{ \"userId\", \"body\" }");
    }

    [Fact]
    public void ParameterTypes_EmittedInGeneratedCode()
    {
        // ParameterTypes dictionary must be present in the generated ToolEntry initializer.
        var code = """
            using Ithil.Attributes;
            using Microsoft.AspNetCore.Mvc;
            public class MyController {
                [AgentTool("desc")]
                [HttpGet("{id}")]
                public void GetItem(int id, string name) {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("ParameterTypes = new Dictionary<string, string>");
        source.Should().Contain("{ \"id\", \"integer\" }");
        source.Should().Contain("{ \"name\", \"string\" }");
    }
}
