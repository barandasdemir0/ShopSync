namespace ShopSync.Web.Dtos;


public sealed class CreateOrderRequestDto
{
    public string? IdempotencyKey { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();

}
