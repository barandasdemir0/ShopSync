using Microsoft.AspNetCore.Mvc;
using ShopSync.OrderService.Dtos;
using ShopSync.OrderService.Infrastructure.GrpcClients;

namespace ShopSync.OrderService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class InventoryController : ControllerBase
{
    private readonly IInventoryGrpcClient _inventoryClient;
    public InventoryController(IInventoryGrpcClient inventoryClient)
    {
        _inventoryClient = inventoryClient;
    }

    [HttpPost("snapshot")]
    public async Task<IActionResult> CreateSnapshot([FromBody] CreateSnapshotDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new 
            { 
                success = false, 
                message = "Snapshot açıklaması boş olamaz." 
            });
        }
        var response = await _inventoryClient.CreateSnapshotAsync(request.Description, ct);

        if (!response.Success)
        {
            return StatusCode(500, response);
        }

        return Ok(response);
    }
    [HttpPost("snapshot/{snapshotId}/restore")]
    public async Task<IActionResult> RestoreSnapshot(string snapshotId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            return BadRequest(new 
            { 
                success = false, message = "SnapshotId boş olamaz." 
            });
        }
        var response = await _inventoryClient.RestoreSnapshotAsync(snapshotId, ct);

        if (!response.Success)
        {
            return StatusCode(500, response);
        }
        return Ok(response);
    }

    [HttpGet("stock/{sku}")]
    public async Task<IActionResult> GetStock(string sku, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return BadRequest(new 
            { 
                success = false, message = "SKU boş olamaz." 
            });
        }
        var response = await _inventoryClient.GetStockAsync(sku, ct);
        if (!response.Found) return NotFound(new 
        { 
            success = false, 
            message = "Ürün stoklarda bulunamadı." 
        });
        return Ok(response);
    }



    [HttpPost("stock/increase")]
    public async Task<IActionResult> IncreaseStock([FromBody] IncreaseStockDto request, CancellationToken ct)
    {
        var response = await _inventoryClient.IncreaseStockAsync(request.Sku, request.Quantity, request.Reason, request.WarehouseCode, ct);
        if (!response.Success)
        {
            return StatusCode(500, response);
        }

        return Ok(response);
    }



    [HttpPost("stock/decrease")]
    public async Task<IActionResult> DecreaseStock([FromBody] DecreaseStockDto request, CancellationToken ct)
    {
        var response = await _inventoryClient.DecreaseStockAsync(request.Sku, request.Quantity, request.Reason, request.WarehouseCode, ct);
        if (!response.Success)
        {
            return StatusCode(500, response);
        }
        return Ok(response);
    }


    [HttpPost("stock/rebalance")]
    public async Task<IActionResult> RebalanceStock([FromBody] RebalanceStockDto request, CancellationToken ct)
    {
        var response = await _inventoryClient.RebalanceStockAsync(request.Sku, request.Quantity, request.FromLocation, request.ToLocation, request.Reason, ct);
        if (!response.Success)
        {
            return StatusCode(500, response);
        }
        return Ok(response);
    }


    [HttpPost("item")]
    public async Task<IActionResult> CreateItem([FromBody] CreateInventoryItemDto request, CancellationToken ct)
    {
        var response = await _inventoryClient.CreateInventoryItemAsync(request.Sku, request.InitialQuantity, request.WarehouseCode, request.LowStockThreshold, ct);
        if (!response.Success)
        {
            return StatusCode(500, response);
        }
        return Ok(response);
    }


    [HttpDelete("item/{warehouseCode}/{sku}")]
    public async Task<IActionResult> DeleteItem(string warehouseCode, string sku, CancellationToken ct)
    {
        var response = await _inventoryClient.DeleteInventoryItemAsync(sku, warehouseCode, ct);
        if (!response.Success)
        {
            return StatusCode(500, response);
        }
        return Ok(response);
    }
}
