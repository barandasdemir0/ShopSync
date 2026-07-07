namespace ShopSync.OrderService.Dtos;

public sealed class IncreaseStockDto
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string WarehouseCode { get; set; } = "DEFAULT"; 
}
