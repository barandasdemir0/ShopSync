namespace ShopSync.OrderService.Dtos;

public sealed class ApiResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? ErrorDetails { get; set; }
}
