using Ithil.Hosting;
using Ithil.Generated;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddIthilHosting();

builder.Services.AddHttpClient("jsonplaceholder", client =>
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/"));

var app = builder.Build();

app.MapControllers();

// Minimal-API endpoints become tools by opting in explicitly with WithAgentTool.
// Mapping a route alone never exposes it to an agent.
app.MapGet("/api/inventory/{sku}", (string sku, bool? includeReserved) =>
        Results.Ok(new { sku, onHand = 42, includeReserved }))
   .WithAgentTool(
        "GetInventory",
        "Returns current stock on hand for a SKU.",
        category: "Inventory");

app.MapPost("/api/inventory/adjust", (StockAdjustment adjustment) =>
        Results.Ok(new { adjustment.Sku, adjusted = true }))
   .WithAgentTool(
        "AdjustInventory",
        "Adjusts stock on hand for a SKU by a signed delta.",
        allowWrite: true,
        category: "Inventory");

// Not marked, so it is invisible to agents.
app.MapGet("/healthz", () => Results.Ok("healthy"));

// The `true` opts in to merging minimal-API endpoints with the source-generated
// [AgentTool] entries. Both appear in a single schema at GET /ithil/schema.
app.MapIthilSchema(SchemaRegistry.Tools, includeMappedEndpoints: true);

app.Run();

/// <summary>Body payload for the AdjustInventory tool.</summary>
public record StockAdjustment(string Sku, int Delta, string? Reason);
