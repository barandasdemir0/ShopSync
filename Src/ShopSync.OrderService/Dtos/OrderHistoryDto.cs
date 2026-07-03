namespace ShopSync.OrderService.Dtos;

public sealed class OrderHistoryDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Reason { get; set; }
}
