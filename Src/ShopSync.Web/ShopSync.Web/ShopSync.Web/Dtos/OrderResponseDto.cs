namespace ShopSync.Web.Dtos;


public sealed class OrderResponseDto
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusDescription { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderLineItemDto> Items { get; set; } = new();
    public List<OrderHistoryDto> History { get; set; } = new();
}
