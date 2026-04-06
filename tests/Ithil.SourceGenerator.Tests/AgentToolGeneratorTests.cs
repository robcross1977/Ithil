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
            [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class)]
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
        diagnostics.Should().Contain(d => d.Id == "ITHIL001");
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
    public void SingleTool_EmitsHttpMethodAndRoutePatternFields()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("HttpMethod");
        source.Should().Contain("RoutePattern");
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
    public void NoHttpAttribute_EmitsEmptyHttpMethod()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc")]
                public void GetInventory() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        // Without an explicit HTTP verb, the generator emits an empty string.
        source.Should().Contain("HttpMethod = \"\"");
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
}
