namespace ShopSync.Web.Dtos;


public sealed class CreateInventoryItemDto
{
    public string Sku { get; set; } = string.Empty;
    public int InitialQuantity { get; set; }
    public string WarehouseCode { get; set; } = "DEFAULT";
    public int LowStockThreshold { get; set; } = 10;
}
