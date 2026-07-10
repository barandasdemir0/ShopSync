namespace ShopSync.Web.Dtos;

public sealed class ForecastResponseDto
{
    public bool Success { get; set; }                     // proto: bool success
    public string Sku { get; set; } = string.Empty;       // proto: string sku
    public int PredictedRequiredQuantity { get; set; }    // proto: int32 predictedRequiredQuantity
    public string Message { get; set; } = string.Empty;   // proto: string message
}
