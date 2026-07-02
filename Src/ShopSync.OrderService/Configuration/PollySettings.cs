namespace ShopSync.OrderService.Configuration;

public sealed class PollySettings
{
    public int RetryCount { get; set; } = 3; // Varsayılan olarak 3 kez yeniden deneme yapılacak
    public int RetryBaseDelayMs { get; set; } = 200; // Varsayılan olarak 200 milisaniye gecikme süresi
    public int CircuitBreakerFailureThreshold { get; set; } = 5; // Varsayılan olarak 5 başarısız deneme sonrası devre kesici tetiklenecek
    public int CircuitBreakerDurationSeconds { get; set; } = 30; // Varsayılan olarak 30 saniye boyunca devre kesici açık kalacak
}
