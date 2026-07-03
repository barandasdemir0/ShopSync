namespace ShopSync.OrderService.Dtos;

public sealed class OrderItemDto
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
