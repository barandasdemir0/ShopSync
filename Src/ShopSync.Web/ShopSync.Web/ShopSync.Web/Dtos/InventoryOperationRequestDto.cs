namespace ShopSync.Web.Dtos;

public sealed class InventoryOperationRequestDto
{
    public string Operation { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Reason { get; set; }
    public string? WarehouseCode { get; set; }
    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }
    public int InitialQuantity { get; set; }
    public int LowStockThreshold { get; set; }
}
