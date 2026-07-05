namespace ShopSync.OrderService.Infrastructure.Idempotency;

public interface IIdempotencyService
{
    // Verilen key daha önce kullanılmış mı kontrol eder. kullanılmamıssa redise yazar true kullanılmısa false döner.
    Task<bool> TryAcquireAsync(string idempotencyKey, TimeSpan expiry, CancellationToken ct = default);


    // İşlem tamamlandığında orderId bilgisini key'e bağlar.
    Task SetOrderIdAsync(string idempotencyKey, string orderId, TimeSpan expiry, CancellationToken ct = default);


    // Verilen key'e bağlı orderId'yi getirir.
    Task<string?> GetOrderIdAsync(string idempotencyKey, CancellationToken ct = default);
}
