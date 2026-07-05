namespace ShopSync.OrderService.Dtos;

public sealed class BatchCancelResponseDto
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<BatchCancelItemResultDto> Results { get; set; } = new();
}
