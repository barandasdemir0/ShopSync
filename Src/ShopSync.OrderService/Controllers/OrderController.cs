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
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
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
    public async Task<IActionResult> CancelOrder(string orderId, [FromBody] CancelOrderRequest? request, CancellationToken ct)
    {
        var response = await _orderService.CancelOrderAsync(orderId, request, ct);
        return Ok(response);
    }

}
