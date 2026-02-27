using FluentAssertions;
using Ithil.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ithil.SourceGenerator.Tests;

public class AgentToolGeneratorTests
{
    // Defines AgentToolAttribute inline so the test compilation is self-contained
    // and ForAttributeWithMetadataName can always resolve it by fully qualified name.
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
