using Ithil.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace SampleApi.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    [AgentTool("Returns current stock level for a given SKU")]
    [HttpGet("stock/{sku}")]
    public IActionResult GetStock(string sku) =>
        Ok(new { Sku = sku, Quantity = 77, Location = "Warehouse A" });

    [AgentTool("Creates a restock order for the given SKU", AllowWrite = true)]
    [HttpPost("restock")]
    public IActionResult CreateStock([FromBody] RestockRequest request) =>
        Ok(new { OrderId = Guid.NewGuid(), Status = "Pending" });
}

public record RestockRequest(string Sku, int Quantity);
