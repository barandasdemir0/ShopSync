namespace ShopSync.OrderService.Dtos;

public sealed class BatchCancelItemResultDto
{
    public string OrderId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
}
