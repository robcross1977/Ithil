# Feature: Roslyn Source Generator (Schema Registry)

## What It Is

A Roslyn Incremental Source Generator that runs at **compile time**, scans every method tagged with `[AgentTool]`, and emits a `SchemaRegistry.g.cs` file containing the complete MCP tool manifest baked into the assembly.

The result: zero runtime reflection, zero startup cost, AOT compatible, and schema errors surface as build errors rather than runtime crashes.

---

## Flow

```mermaid
flowchart TD
    A[Developer writes controller\ndecorated with AgentTool] --> B[dotnet build runs]
    B --> C[Roslyn parses syntax tree]
    C --> D[Source Generator: ForAttributeWithMetadataName\nfinds all AgentTool methods]
    D --> E[GetToolMetadata extracts\nname, params, description, scopes, etc.]
    E --> F[GenerateSchemaRegistry\nbuilds C# source string]
    F --> G[SchemaRegistry.g.cs emitted\ninto compilation]
    G --> H[Assembly contains\nSchemaRegistry.Tools at runtime]
    H --> I[/.well-known/mcp reads\nSchemaRegistry.Tools\nand returns manifest]
```

---

## What Gets Generated

Given this input:
```
[AgentTool("Returns stock levels for a SKU", RequiredScopes = ["inventory.read"])]
public async Task<IActionResult> GetInventory(int productId, string warehouseId)
```

The generator emits a static registry entry for `GetInventory` with:
- Name derived from method name
- Description from attribute constructor
- Parameters with JSON types inferred from C# types (`int` → `"integer"`, `string` → `"string"`)
- Required params = all non-nullable, non-optional parameters
- AllowWrite, MaxResponseTokens, Category from attribute

---

## Acceptance Criteria

- [ ] Generator runs without errors on a project with zero `[AgentTool]` methods (emits empty registry)
- [ ] Generator correctly extracts method name, description, AllowWrite, MaxResponseTokens, Category
- [ ] C# `int` parameters map to JSON Schema `"integer"`
- [ ] C# `string` parameters map to JSON Schema `"string"`
- [ ] C# `bool` parameters map to JSON Schema `"boolean"`
- [ ] C# `DateOnly` and `DateTime` parameters map to JSON Schema `"string"` with `"format": "date"`
- [ ] Non-nullable parameters appear in the `required` array
- [ ] Nullable parameters (`int?`, `string?`) do NOT appear in `required`
- [ ] A method decorated at the class level inherits the attribute for all its public methods
- [ ] Generator targets `net8.0` and `net9.0` — verified with both TFMs
- [ ] Adding a new `[AgentTool]` method triggers incremental regeneration (only changed files reprocessed)
- [ ] A non-public method with `[AgentTool]` produces a build warning (DiagnosticSeverity.Warning)

---

## Files & Functions

```
Ithil.SourceGenerator/
├── AgentToolGenerator.cs
│   └── class AgentToolGenerator : IIncrementalGenerator
│       ├── Initialize(IncrementalGeneratorInitializationContext) → void
│       │   Sets up: ForAttributeWithMetadataName("Ithil.Attributes.AgentToolAttribute")
│       │            predicate: node is MethodDeclarationSyntax
│       │            transform: GetToolMetadata()
│       │            RegisterSourceOutput → GenerateSchemaRegistry()
│       │
│       ├── GetToolMetadata(GeneratorAttributeSyntaxContext, CancellationToken) → ToolMetadata
│       │   Extracts: method name, parameter names/types, attribute values
│       │
│       └── GenerateSchemaRegistry(SourceProductionContext, ImmutableArray<ToolMetadata>) → void
│           Builds: SchemaRegistry.g.cs source string
│           Calls: AddSource("SchemaRegistry.g.cs", source)
│
├── ToolMetadata.cs
│   └── record ToolMetadata
│       ├── string MethodName
│       ├── string Description
│       ├── bool AllowWrite
│       ├── int MaxResponseTokens
│       ├── string? Category
│       ├── string[] RequiredScopes
│       ├── Seq<ParameterMetadata> Parameters
│       └── Seq<string> RequiredParams
│
├── ParameterMetadata.cs
│   └── record ParameterMetadata
│       ├── string Name
│       ├── string JsonType       ("string" | "integer" | "boolean" | "number")
│       └── string? Description
│
└── TypeMapper.cs
    └── static class TypeMapper
        └── ToJsonType(ITypeSymbol typeSymbol) → string
            Maps C# type symbols to JSON Schema type strings

Ithil.SourceGenerator.Tests/
└── AgentToolGeneratorTests.cs
    (Uses Microsoft.CodeAnalysis.CSharp.Testing helpers)
```

---

## Unit Testing Plan

Tests live in `Ithil.SourceGenerator.Tests/`. Use `Microsoft.CodeAnalysis.CSharp.Testing` or `CSharpSourceGeneratorTest<T>` for in-process generator testing — no separate build process needed.

### Test: EmptyProject_EmitsEmptyRegistry
- Run generator on source with no `[AgentTool]` attributes
- Assert generated `SchemaRegistry.Tools` is an empty collection

### Test: SingleMethod_EmitsCorrectName
- Input: `[AgentTool("desc")] public Task<IActionResult> GetInventory(...)`
- Assert generated source contains `Name = "GetInventory"`

### Test: SingleMethod_EmitsDescription
- Assert generated source contains `Description = "desc"`

### Test: IntParameter_MapsToInteger
- Input: method with `int productId` parameter
- Assert generated schema has `"type": "integer"` for `productId`

### Test: StringParameter_MapsToString
- Input: method with `string warehouseId` parameter
- Assert `"type": "string"` for `warehouseId`

### Test: NonNullableParam_IsInRequired
- Input: method with `int productId` (non-nullable)
- Assert `"productId"` appears in `required` array

### Test: NullableParam_IsNotInRequired
- Input: method with `int? quantity` (nullable)
- Assert `"quantity"` does NOT appear in `required` array

### Test: AllowWrite_DefaultsFalse
- No `AllowWrite` set on attribute
- Assert `AllowWrite = false` in generated source

### Test: AllowWrite_CanBeSetTrue
- `[AgentTool("desc", AllowWrite = true)]`
- Assert `AllowWrite = true` in generated source

### Test: MultipleTools_AllEmitted
- Two methods decorated with `[AgentTool]`
- Assert both appear in `SchemaRegistry.Tools`

### Test: TypeMapper_DateOnly_MapsToStringWithFormat
- Input: `DateOnly from` parameter
- Assert JSON type is `"string"` and format is `"date"`

### Test: NonPublicMethod_EmitsDiagnosticWarning
- Input: `[AgentTool("desc")] private Task<IActionResult> HiddenTool()`
- Assert generator emits a diagnostic with `DiagnosticSeverity.Warning`
