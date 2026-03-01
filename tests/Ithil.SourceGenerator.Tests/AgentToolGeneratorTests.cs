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
    public void EmptyProject_EmitsEmptyRegistry()
    {
        var (_, _, source) = RunGenerator("// no tools here");

        source.Should().Contain("SchemaRegistry");
        source.Should().Contain("Tools");
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
    public void AllowWrite_DefaultsFalse()
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
    public void AllowWrite_CanBeSetTrue()
    {
        var code = """
            using Ithil.Attributes;
            public class MyController {
                [AgentTool("desc", AllowWrite = true)]
                public void DeleteItem() {}
            }
            """;

        var (_, _, source) = RunGenerator(code);

        source.Should().Contain("AllowWrite = true");
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
        diagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Warning);
    }
}
