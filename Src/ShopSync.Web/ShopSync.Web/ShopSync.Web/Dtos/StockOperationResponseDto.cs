namespace ShopSync.Web.Dtos;

public sealed class StockOperationResponseDto
{
    public bool Success { get; set; }      // proto: bool success
    public string Message { get; set; } = string.Empty;  // proto: string message
}
