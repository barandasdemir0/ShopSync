namespace ShopSync.Web.Dtos;


public sealed class OrderLineItemDto
{
    public string Sku { get; set; } = string.Empty;
    public int RequestedQuantity { get; set; }
    public int ReservedQuantity { get; set; }
}
