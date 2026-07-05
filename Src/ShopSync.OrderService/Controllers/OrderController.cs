using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShopSync.OrderService.Dtos;
using ShopSync.OrderService.Repositories;
using ShopSync.OrderService.Services;

namespace ShopSync.OrderService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderController : ControllerBase
{
    private readonly IOrderAppService _orderService;
    public OrderController(IOrderAppService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequestDto request, CancellationToken ct)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest("Sipariş en az bir ürün içermelidir.");
        }

        var response = await _orderService.CreateOrderAsync(request, ct);
        return CreatedAtAction(nameof(GetOrder), new 
        {
            orderId = response.OrderId 
        }, response);
    }
    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(string orderId, CancellationToken ct)
    {
        var response = await _orderService.GetOrderAsync(orderId, ct);
        return Ok(response);
    }
    [HttpGet]
    public async Task<IActionResult> ListOrders([FromQuery] OrderFilter filter, CancellationToken ct)
    {
        var response = await _orderService.ListOrdersAsync(filter, ct);
        return Ok(response);
    }

    [HttpDelete("{orderId}")]
    public async Task<IActionResult> CancelOrder(string orderId, [FromBody] CancelOrderRequestDto? request, CancellationToken ct)
    {
        var response = await _orderService.CancelOrderAsync(orderId, request, ct);
        return Ok(response);
    }

    [HttpPost("{orderId}/confirm")]
    public async Task<IActionResult> ConfirmOrder(string orderId,[FromBody] ConfirmOrderRequestDto? request ,CancellationToken ct)
    {
        var response = await _orderService.ConfirmOrderAsync(orderId, ct);
        return Ok(response);
    }


    [HttpPost("batch-cancel")]
    public async Task<IActionResult> BatchCancel([FromBody] BatchCancelRequestDto request,
    CancellationToken ct)
    {
        if (request.OrderIds.Count == 0)
        {
            return BadRequest(new 
            { 
                success = false, message = "En az bir sipariş ID'si belirtilmelidir." 
            });
        }
           
        var response = await _orderService.BatchCancelAsync(request, ct);
        return Ok(response);
    }


    [HttpPost("{orderId}/admin-override")]
    public async Task<IActionResult> AdminOverride(string orderId, [FromBody] AdminOverrideRequestDto request,
    CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new 
            { 
                success = false, message = "Admin override için sebep belirtilmelidir." 
            });
        }
            
        var response = await _orderService.AdminOverrideCancelAsync(orderId, request.Reason, ct);
        return Ok(response);
    }

}
