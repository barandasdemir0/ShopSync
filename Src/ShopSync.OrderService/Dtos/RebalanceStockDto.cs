namespace ShopSync.OrderService.Dtos;

public sealed class RebalanceStockDto
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
