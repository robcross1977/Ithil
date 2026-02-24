# Feature: AgentTool Attribute

## What It Is

A C# attribute that developers place on controller methods to mark them as MCP-callable tools. The attribute carries metadata — description, required scopes, write permission flag, token limit, and category grouping — that the Source Generator reads at compile time to build the tool manifest.

This is the developer-facing API surface. Everything else in Ithil is invisible plumbing; `[AgentTool]` is what a developer actually touches.

---

## Flow

```mermaid
flowchart TD
    A[Developer decorates\ncontroller method with AgentTool] --> B[Attribute stores metadata\ndescription, scopes, AllowWrite, MaxTokens, Category]
    B --> C[Source Generator reads attribute\nat compile time]
    C --> D[SchemaRegistry.g.cs\nemitted with MCP tool definition]
    D --> E[/.well-known/mcp\nserves the manifest]
    E --> F[AI Agent reads manifest\nand knows what tools exist]
```

---

## Acceptance Criteria

- [ ] `[AgentTool("description")]` compiles on a controller method with no other arguments
- [ ] `RequiredScopes`, `AllowWrite`, `MaxResponseTokens`, and `Category` are all optional
- [ ] `AllowWrite` defaults to `false` (read-only by default — destructive methods require opt-in)
- [ ] `MaxResponseTokens` defaults to `2000`
- [ ] Attribute can be applied to both methods and classes (class-level applies to all methods)
- [ ] Two methods on the same controller can each have different `[AgentTool]` configurations
- [ ] Applying the attribute to a non-public method produces a build warning from the Source Generator

---

## Files & Functions

```
Ithil.Attributes/
└── AgentToolAttribute.cs
    └── class AgentToolAttribute : Attribute
        ├── string Description        (constructor param, required)
        ├── string[]? RequiredScopes  (init, optional)
        ├── bool AllowWrite           (init, default false)
        ├── int MaxResponseTokens     (init, default 2000)
        └── string? Category          (init, optional)

Ithil.Core/
└── Models/
    └── McpToolDefinition.cs
        └── record McpToolDefinition
            ├── string Name
            ├── string Description
            ├── bool AllowWrite
            ├── int MaxResponseTokens
            ├── string? Category
            ├── string[] RequiredScopes
            └── McpInputSchema InputSchema

    └── McpInputSchema.cs
        └── record McpInputSchema
            ├── Map<string, JsonSchemaProperty> Properties
            └── Seq<string> Required
```

---

## Unit Testing Plan

Write these tests before writing the attribute. Tests live in `Ithil.Attributes.Tests/`.

### Test: DefaultValues
- Create `AgentToolAttribute` with only a description string
- Assert `AllowWrite == false`
- Assert `MaxResponseTokens == 2000`
- Assert `RequiredScopes == null`
- Assert `Category == null`

### Test: DescriptionIsStored
- Create attribute with `"Returns stock levels for a SKU"`
- Assert `Description == "Returns stock levels for a SKU"`

### Test: AllowWriteCanBeEnabled
- Create attribute with `AllowWrite = true`
- Assert `AllowWrite == true`

### Test: RequiredScopesAreStored
- Create attribute with `RequiredScopes = ["inventory.read", "orders.read"]`
- Assert `RequiredScopes` contains both values

### Test: CategoryIsStored
- Create attribute with `Category = "Inventory"`
- Assert `Category == "Inventory"`

### Test: MaxResponseTokensCanBeOverridden
- Create attribute with `MaxResponseTokens = 500`
- Assert `MaxResponseTokens == 500`

### Test: AttributeTargetsAreMethodAndClass
- Verify `AttributeUsage` includes `AttributeTargets.Method | AttributeTargets.Class`
- This is a reflection test on the attribute's own `AttributeUsage` metadata
