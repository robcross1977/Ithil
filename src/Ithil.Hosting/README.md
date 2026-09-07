# Ithil.Hosting

Add this package to your existing ASP.NET Web API to expose your endpoints to the [Ithil Gateway](https://ithil.software) as MCP tools.

Endpoints become tools two ways — `[AgentTool]` on controller actions, discovered at compile time, and `WithAgentTool()` on minimal-API routes, discovered at startup. Both appear in a single schema.

## Installation

```bash
dotnet add package Ithil.Hosting
```

## Controllers

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddIthilHosting();

var app = builder.Build();

app.MapControllers();
app.MapIthilSchema(SchemaRegistry.Tools);

app.Run();
```

`SchemaRegistry` is generated at compile time by the Ithil source generator. It contains one entry for every method decorated with `[AgentTool]`.

## Minimal APIs

Mark a mapped endpoint with `WithAgentTool()`, then pass `includeMappedEndpoints: true` so both discovery paths are merged into one schema:

```csharp
app.MapGet("/api/inventory/{sku}", (string sku, bool? includeReserved) => ...)
   .WithAgentTool("GetInventory", "Returns current stock on hand for a SKU.");

app.MapPost("/api/inventory/adjust", (StockAdjustment adjustment) => ...)
   .WithAgentTool("AdjustInventory", "Adjusts stock for a SKU.", allowWrite: true);

// Unmarked, so agents never see it.
app.MapGet("/healthz", () => Results.Ok("healthy"));

app.MapIthilSchema(SchemaRegistry.Tools, includeMappedEndpoints: true);
```

Marking is opt-in: an endpoint without `WithAgentTool()` is never exposed, so adding a route can never silently widen what agents can reach.

Route templates, verbs and parameter binding are read from the routing table after startup, so route groups and prefixes resolve on their own. Parameters bound from the path, the query string and the request body are classified automatically, and framework-supplied parameters (`CancellationToken`, `HttpContext`, `[FromServices]`, `[FromHeader]`) are excluded.

### Choosing a verb

A tool carries exactly one HTTP method. When an endpoint does not resolve to exactly one — a verbless `app.Map(...)`, or a multi-verb `app.MapMethods(...)` — say which to use:

```csharp
app.MapMethods("/api/things", ["GET", "POST"], handler)
   .WithAgentTool("CreateThing", "Creates a thing", allowWrite: true, httpMethod: "POST");
```

Leaving it ambiguous throws at startup rather than publishing a tool an agent cannot call.

## Documentation

Full documentation at [ithil.software/installation/downstream](https://ithil.software/installation/downstream).
