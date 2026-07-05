using ShopSync.OrderService.Models;

namespace ShopSync.OrderService.Repositories;

public interface IOrderRepository
{
    // Yeni sipariş oluşturur
    Task InsertAsync(Order order, CancellationToken ct = default);

    // Sipariş günceller (durum değişiklikleri vb.)
    Task UpdateAsync(Order order, CancellationToken ct = default);

    // OrderId ile sipariş getirir
    Task<Order?> GetByOrderIdAsync(string orderId, CancellationToken ct = default);

    // MongoDB ObjectId ile sipariş getirir background jobs işlemleri için
    Task<Order?> GetByIdAsync(string id, CancellationToken ct = default);

    // Duruma ve tarih aralığına göre siparişleri listeler 
    Task<List<Order>> ListOrdersAsync(OrderFilter filter, CancellationToken ct = default);

    // Belirli bir idempotency key ile daha önce sipariş oluşturulmuş mu?
    Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    // Birden fazla OrderId ile sipariş getirir 
    Task<List<Order>> GetByOrderIdsAsync(IEnumerable<string> orderIds, CancellationToken ct = default);

    //  Belirli bir tarihten eski PENDING siparişleri getirir
    Task<List<Order>> GetExpiredOrdersAsync(DateTime olderThan, CancellationToken ct = default);


    // Belirli bir tarihten önceki durum bazlı siparişleri getirir.
    Task<List<Order>> GetOrdersByStatusBeforeDateAsync(string status, DateTime cutoffTime, CancellationToken ct = default);

}


public record OrderFilter(
    string? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20
);
