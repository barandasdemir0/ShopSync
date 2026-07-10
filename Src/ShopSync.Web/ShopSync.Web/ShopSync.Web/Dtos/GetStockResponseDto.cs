namespace ShopSync.Web.Dtos;

public sealed class GetStockResponseDto
{
    public string Sku { get; set; } = string.Empty;       // proto: string sku
    public int AvailableQuantity { get; set; }            // proto: int32 available_quantity
    public int ReservedQuantity { get; set; }             // proto: int32 reserved_quantity
    public bool Found { get; set; }                       // proto: bool found
}
