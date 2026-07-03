namespace ShopSync.OrderService.Dtos;

public sealed class CreateOrderRequest
{
    public string? IdempotencyKey { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();

}
