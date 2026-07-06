namespace ShopSync.OrderService.Configuration;

public sealed class RateLimitingSettings
{
    // Maksimum izin verilen istek sayısı
    public int PermitLimit { get; set; } = 100;

    // Zaman penceresi (saniye cinsinden)
    public int WindowSeconds { get; set; } = 60;

    // Limiti aşan ama hemen reddedilmeyip kuyrukta işlenmeyi bekleyecek istek sayısı
    public int QueueLimit { get; set; } = 5;
}
