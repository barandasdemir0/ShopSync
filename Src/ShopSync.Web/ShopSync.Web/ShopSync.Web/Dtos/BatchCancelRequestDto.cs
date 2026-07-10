namespace ShopSync.Web.Dtos;


public sealed class BatchCancelRequestDto
{
    // İptal edilecek sipariş ID'leri
    public List<string> OrderIds { get; set; } = new();

    // Toplu iptal sebebi
    public string? Reason { get; set; }
}
