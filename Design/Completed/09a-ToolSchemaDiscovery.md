# Feature: Tool Schema Discovery & Routing

> **Priority: CRITICAL — do this before everything else.**
> Nothing else in the system is useful until an AI agent can actually discover and call tools.
> Without this, `tools/list` returns empty and `tools/call` returns nothing.

---

## What It Is

The bridge between the Roslyn source generator and the MCP protocol.

Right now the generator correctly compiles tool metadata into `SchemaRegistry.g.cs` inside the downstream service (SampleApi). But the gateway has no way to see that data. `ToolsListHandler` returns a hardcoded empty array. `ToolsCallHandler` returns hardcoded empty content.

This feature closes that gap end-to-end:

1. **Generator Enhancement** — also capture the HTTP method and route pattern for each tool (from `[HttpGet]`, `[HttpPost]`, `[Route]` etc.) so the gateway knows how to call it
2. **Ithil.Hosting project** — a lightweight NuGet-ready library the downstream service adds. One line: `app.MapIthilSchema()`. Exposes `GET /ithil/schema` returning all tool definitions including routing info
3. **Gateway Tool Registry** — fetches and caches the downstream schema. `ToolsListHandler` reads it and returns real tool definitions to the AI
4. **Gateway Tool Call Router** — when an AI calls `tools/call`, looks up the tool's HTTP route and proxies the call to the downstream via `HttpClient`

When this feature is done, a developer can:
- Decorate a controller method with `[AgentTool("...")]`
- Add `app.MapIthilSchema()` to their startup
- Point the gateway at their service
- Connect Claude (or any MCP-compatible AI) and have it discover and call the tool

---

## Flow

```mermaid
flowchart TD
    A[Developer adds AgentTool attribute\nand HTTP route attribute to method] --> B[dotnet build]
    B --> C[Source Generator reads AgentTool\nAND HttpGet/Post/Route attributes]
    C --> D[Emits SchemaRegistry.g.cs with\nName, Description, InputSchema\nHttpMethod, RoutePattern, ParameterSources]

    D --> E[SampleApi starts up\napp.MapIthilSchema called]
    E --> F[GET /ithil/schema endpoint registered\nreturns SchemaRegistry.Tools as JSON]

    G[AI agent connects to Gateway\nPOST /mcp: initialize] --> H[Gateway: InitializeHandler\nreturns capabilities]
    H --> I[AI: POST /mcp: tools/list]
    I --> J[ToolsListHandler calls IToolRegistry]
    J --> K[ToolRegistryService: GET /ithil/schema\nfetches + caches tool list]
    K --> L[Returns MCP tool definitions\nname + description + inputSchema only\nrouting info stripped - internal only]
    L --> M[AI sees available tools]

    M --> N[AI: POST /mcp: tools/call\nname=GetStock, arguments={sku:ABC}]
    N --> O[ToolsCallHandler looks up GetStock\nin cached registry]
    O --> P[Finds: GET api/inventory/stock/{sku}\nsku=route param]
    P --> Q[Builds HTTP request:\nGET http://localhost:5200/api/inventory/stock/ABC]
    Q --> R[HttpClient sends request to downstream]
    R --> S[Response body returned as\nMCP tools/call content]
```

---

## Architecture Decisions

### How does the gateway discover tools?

The downstream service exposes `GET /ithil/schema` (registered by `MapIthilSchema()`). The gateway fetches this endpoint on first use and caches the result in memory. This is a pull model — simple, no startup coupling, works across restarts.

### How does the generator share types with the gateway?

It doesn't — and that's correct. The generator compiles `ToolEntry` into the downstream service. The gateway defines its own `ToolRegistryEntry` in `Ithil.Core`. They share a **JSON contract** (same property names/types), not a C# type. This is exactly how microservices communicate. No circular dependencies.

### Where does routing live?

Routing info (`HttpMethod`, `RoutePattern`, `ParameterSources`) is captured by the generator and returned from `/ithil/schema`. The gateway uses it internally for `tools/call` routing. It is **never exposed to the AI agent** — the AI only sees name, description, and inputSchema. This keeps the MCP surface clean and prevents AI agents from making arbitrary HTTP calls.

### Parameter binding

The generator infers how each parameter is bound by checking:
1. Does the parameter name appear in the route template `{paramName}`? → route
2. Does the parameter have `[FromBody]`? → body
3. Does the parameter have `[FromQuery]`? → query
4. Is it a simple type (string, int, bool, etc.)? → query (ASP.NET default)
5. Is it a complex type? → body (ASP.NET default)

---

## Other Gaps Found During Audit

These are separate from the main feature but should be fixed in the same sprint:

### Gap A: ResponseTransformPipeline is never called
`Program.cs` only registers `RequestTransformPipeline` in the YARP transform context. `ResponseTransformPipeline` exists and is in DI but has no call site — meaning PII scrubbing, token recording, and trace emission on responses are all no-ops right now.

Fix: register a response transform in `Program.cs` alongside the request transform.

### Gap B: IToolAllowlistService always returns true
`NotImplementedToolAllowlistService` passes every tool for every agent. We have `AgentConfig.AllowedTools` on the model but never check it. This is a one-file fix — replace the stub with a real implementation that checks `AgentConfig.AllowedTools`.

---

## Acceptance Criteria

- [ ] Developer adds `[AgentTool]` + `[HttpGet("route")]` to a method, builds, and `GET /ithil/schema` returns that tool with correct name, description, inputSchema, httpMethod, and routePattern
- [ ] Gateway `POST /mcp` with `tools/list` returns the real tool list with name, description, inputSchema — not an empty array
- [ ] Gateway `POST /mcp` with `tools/call {"name": "GetStock", "arguments": {"sku": "ABC"}}` calls `GET /downstream/api/inventory/stock/ABC` and returns the response content
- [ ] Gateway `POST /mcp` with `tools/call {"name": "CreateStock", "arguments": {"Sku": "ABC", "Quantity": 10}}` calls `POST /downstream/api/inventory/restock` with JSON body and returns the response
- [ ] Route parameters are substituted correctly: `{sku}` in pattern is replaced with argument value
- [ ] Unknown tool name in `tools/call` returns a JSON-RPC error (not a 500)
- [ ] Schema is cached in memory — downstream is only called once per gateway process lifetime (not on every `tools/list` request)
- [ ] ResponseTransformPipeline is wired and PII scrubbing runs on downstream responses
- [ ] IToolAllowlistService checks AgentConfig.AllowedTools (empty list = allow all)

---

## Files & Functions

### Generator Changes (`src/Ithil.SourceGenerator/`)

```
AgentToolGenerator.cs
└── ExtractToolMetadata()
    ├── ADD: ReadHttpMethodAttribute(IMethodSymbol) → (string httpMethod, string routeTemplate)
    │   Reads [HttpGet("...")], [HttpPost("...")], [HttpPut], [HttpDelete], [HttpPatch]
    └── ADD: ReadClassRoutePrefix(IMethodSymbol) → string
        Reads [Route("...")] on the containing class

ToolMetadata.cs
└── ADD fields:
    ├── string HttpMethod       ("GET" | "POST" | "PUT" | "DELETE" | "PATCH")
    ├── string RoutePattern     (full combined route e.g. "api/inventory/stock/{sku}")
    └── Dictionary<string, string> ParameterSources  ({"sku": "route", "warehouseId": "query"})

AgentToolGenerator.cs
└── GenerateSchemaRegistry()
    └── ADD: emit HttpMethod, RoutePattern, ParameterSources to ToolEntry
```

### New Project: `src/Ithil.Hosting/`

```
Ithil.Hosting.csproj
└── References: Ithil.Core (for McpToolDefinition, McpInputSchema, JsonSchemaProperty)

WebApplicationExtensions.cs
└── static MapIthilSchema(this WebApplication app) → IEndpointRouteBuilder
    Registers: GET /ithil/schema
    Handler: reads SchemaRegistry.Tools, formats as ToolSchemaResponse[], returns JSON
    Note: SchemaRegistry is referenced by the consuming project (SampleApi) at compile time

ToolSchemaResponse.cs   (the JSON contract shape returned from /ithil/schema)
└── record ToolSchemaResponse
    ├── string Name
    ├── string Description
    ├── bool AllowWrite
    ├── int MaxResponseTokens
    ├── string? Category
    ├── string HttpMethod
    ├── string RoutePattern
    ├── Dictionary<string, string> ParameterSources  (name → "route" | "query" | "body")
    └── McpInputSchema InputSchema
```

### New Core Types (`src/Ithil.Core/`)

```
Interfaces/IToolRegistry.cs
└── interface IToolRegistry
    └── GetToolsAsync(CancellationToken) → Task<Seq<ToolRegistryEntry>>

Models/ToolRegistryEntry.cs   (gateway-side counterpart of ToolSchemaResponse)
└── record ToolRegistryEntry
    ├── string Name
    ├── string Description
    ├── bool AllowWrite
    ├── int MaxResponseTokens
    ├── string? Category
    ├── string HttpMethod
    ├── string RoutePattern
    ├── Dictionary<string, string> ParameterSources
    └── McpInputSchema InputSchema
```

### Gateway Changes (`src/Ithil.Gateway/`)

```
Mcp/
├── ToolRegistryService.cs   (implements IToolRegistry)
│   ├── Constructor: IHttpClientFactory, ToolRegistryOptions
│   ├── GetToolsAsync() → fetch /ithil/schema, deserialize, cache in memory
│   └── Field: _cachedTools (Seq<ToolRegistryEntry>?, populated on first call)
│
├── ToolRegistryOptions.cs
│   └── string DownstreamSchemaUrl  (e.g. "http://localhost:5200/ithil/schema")
│
├── ToolCallRouter.cs
│   ├── BuildRequest(ToolRegistryEntry tool, JsonElement arguments) → HttpRequestMessage
│   │   Substitutes route params, adds query params, serializes body
│   └── RouteParamNames(string routePattern) → IEnumerable<string>
│       Extracts {paramName} tokens from a route pattern
│
└── Handlers/
    ├── ToolsListHandler.cs   (replace stub with real implementation)
    │   └── HandleAsync(JsonRpcRequest, IToolRegistry) → Task<JsonRpcResponse>
    │       Calls GetToolsAsync(), formats as MCP tools/list result
    │       Strips HttpMethod/RoutePattern from public response
    │
    └── ToolsCallHandler.cs   (replace stub with real implementation)
        └── HandleAsync(JsonRpcRequest, IToolRegistry, IHttpClientFactory) → Task<JsonRpcResponse>
            Deserializes params.name + params.arguments
            Looks up tool in registry
            Calls ToolCallRouter.BuildRequest()
            Sends via HttpClient
            Wraps response as MCP content

Stubs/NotImplementedStubs.cs
└── REPLACE: NotImplementedToolAllowlistService
    └── Real implementation: checks AgentConfig.AllowedTools
        (empty list = allow all; non-empty = must contain tool name)

ServiceCollectionExtensions.cs
└── ADD: Register IToolRegistry → ToolRegistryService (Singleton - caches tools)
    ADD: Register IHttpClientFactory (AddHttpClient)
    ADD: Register ToolRegistryOptions from config

Program.cs
└── ADD: Response transform registration in YARP pipeline
    (mirrors the existing request transform registration)
```

### SampleApi Changes (`samples/SampleApi/`)

```
SampleApi.csproj
└── ADD: <ProjectReference Include="..\..\src\Ithil.Hosting\Ithil.Hosting.csproj" />

Program.cs
└── ADD: app.MapIthilSchema();

appsettings.json  (gateway side)
└── ADD: "Ithil": { "ToolRegistry": { "DownstreamSchemaUrl": "http://localhost:5200/ithil/schema" } }
```

---

## Unit Testing Plan

### Generator Tests (`Ithil.SourceGenerator.Tests/`)

#### Test: HttpGet_CapturesMethodAndRoute
- Input: `[AgentTool("desc")] [HttpGet("stock/{sku}")] public IActionResult GetStock(string sku)`
- Class has `[Route("api/inventory")]`
- Assert emitted `ToolEntry` has `HttpMethod = "GET"`, `RoutePattern = "api/inventory/stock/{sku}"`

#### Test: HttpPost_CapturesMethodAndRoute
- Input: `[AgentTool("desc")] [HttpPost("restock")] public IActionResult CreateStock(...)`
- Assert `HttpMethod = "POST"`, `RoutePattern = "api/inventory/restock"`

#### Test: RouteParam_SourceIsRoute
- Input: `[HttpGet("items/{id}")] public IActionResult Get(int id)`
- Assert `ParameterSources["id"] == "route"`

#### Test: SimpleTypeParam_WithoutRouteTemplate_SourceIsQuery
- Input: `[HttpGet("items")] public IActionResult Get(string filter)`
- `filter` not in route template
- Assert `ParameterSources["filter"] == "query"`

#### Test: ComplexTypeParam_SourceIsBody
- Input: `[HttpPost("create")] public IActionResult Create(CreateRequest request)`
- Assert `ParameterSources["request"] == "body"`

#### Test: FromBodyAttribute_OverridesInference
- Input: `[HttpPost("create")] public IActionResult Create([FromBody] string raw)`
- Assert `ParameterSources["raw"] == "body"` even though string is normally query

#### Test: NoHttpAttribute_RoutePatternIsEmpty
- Input: `[AgentTool("desc")] public IActionResult NoRoute()`
- Assert `RoutePattern == ""` and `HttpMethod == ""`

---

### ToolCallRouter Tests (`Ithil.Gateway.Tests/`)

#### Test: RouteParam_IsSubstitutedInPath
- Tool: `RoutePattern = "api/inventory/stock/{sku}"`, `HttpMethod = "GET"`, `ParameterSources = {"sku": "route"}`
- Arguments: `{"sku": "ABC-123"}`
- Assert built URL is `api/inventory/stock/ABC-123`

#### Test: QueryParam_IsAppendedToUrl
- Tool: `RoutePattern = "api/items"`, `HttpMethod = "GET"`, `ParameterSources = {"filter": "query"}`
- Arguments: `{"filter": "active"}`
- Assert built URL is `api/items?filter=active`

#### Test: BodyParam_IsSerializedAsJsonBody
- Tool: `RoutePattern = "api/items"`, `HttpMethod = "POST"`, `ParameterSources = {"request": "body"}`
- Arguments: `{"request": {"Sku": "ABC", "Quantity": 10}}`
- Assert `HttpRequestMessage.Content` is JSON with `{"Sku":"ABC","Quantity":10}`

#### Test: UnknownTool_ReturnsError
- `tools/call` with name that doesn't exist in registry
- Assert `JsonRpcResponse.Error` is not null, code = -32602 (invalid params)

---

### ToolRegistryService Tests (`Ithil.Gateway.Tests/`)

#### Test: GetTools_CallsDownstreamSchemaEndpoint
- Mock `HttpClient` returns a valid `ToolSchemaResponse[]` JSON
- Assert `GetToolsAsync()` returns populated `Seq<ToolRegistryEntry>`

#### Test: GetTools_CachesResultAfterFirstCall
- Call `GetToolsAsync()` twice
- Assert HttpClient only called once (second call uses cache)

#### Test: GetTools_ReturnsEmpty_WhenDownstreamUnreachable
- Mock `HttpClient` throws `HttpRequestException`
- Assert `GetToolsAsync()` returns empty Seq (fail-open)

---

### ToolsListHandler Tests (`Ithil.Gateway.Tests/`)

#### Test: ToolsList_ReturnsRealTools_WhenRegistryHasEntries
- Registry returns two tools
- Assert response `Result.tools` has two entries with correct name/description/inputSchema
- Assert `HttpMethod` and `RoutePattern` are NOT present in the response (internal only)

#### Test: ToolsList_ReturnsEmpty_WhenRegistryIsEmpty
- Registry returns no tools
- Assert response `Result.tools` is empty array (not an error)
