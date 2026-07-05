using ShopSync.InventoryService.Protos;
using ShopSync.OrderService.Infrastructure.DeadLetter;
using ShopSync.OrderService.Infrastructure.GrpcClients;
using ShopSync.OrderService.Models;
using ShopSync.OrderService.Repositories;

namespace ShopSync.OrderService.BackgroundJobs;

public sealed class OrderExpirationJob : BackgroundService
{

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderExpirationJob> _logger;

    // Kontrol aralığı: 2 dakikada bir çalışır
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2);

    // Sipariş süresi: 10 dakika içinde onaylanmazsa expire olur
    private readonly TimeSpan _expirationThreshold = TimeSpan.FromMinutes(10);

    // Retry limiti: Bir sipariş kaç kez expire denenecek (başarısız olursa DLQ)
    private const int MaxRetryAttempts = 3;


    public OrderExpirationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderExpirationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
             "OrderExpirationJob başlatıldı. Kontrol aralığı: {Interval} dk, Süre sınırı: {Threshold} dk",
             _checkInterval.TotalMinutes, _expirationThreshold.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredOrdersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "OrderExpirationJob döngüsünde beklenmeyen hata.");
            }
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }


    private async Task ProcessExpiredOrdersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var inventoryClient = scope.ServiceProvider.GetRequiredService<IInventoryGrpcClient>();
        var deadLetterService = scope.ServiceProvider.GetRequiredService<IDeadLetterService>();


        // 10 dakikadan eski PENDING siparişleri getir
        var cutoffTime = DateTime.UtcNow.Subtract(_expirationThreshold);
        var expiredOrders = await orderRepository.GetOrdersByStatusBeforeDateAsync(
            OrderStatus.Pending.Code, cutoffTime, ct);


        if (expiredOrders.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Süresi dolmuş {Count} sipariş bulundu. İşleniyor...", expiredOrders.Count);
        var successCount = 0;
        var failedCount = 0;

        foreach (var order in expiredOrders)
        {
            try
            {
                order.Expire();

                // 2. InventoryService'e stokları serbest bırak
                var releaseItems = order.LineItems.Select(li =>
                    new ReservationItem
                    {
                        Sku = li.Sku,
                        Quantity = li.RequestedQuantity
                    });

                var releaseResponse = await inventoryClient.ReleaseBatchAsync(
               order.OrderId, releaseItems, ct);

                if (!releaseResponse.Success)
                {
                    _logger.LogWarning(
                        "ReleaseBatch başarısız. OrderId: {OrderId}. " +
                        "Sipariş yine de expire edildi, reconciliation job düzeltecek.",
                        order.OrderId);
                }

                // 3. MongoDB'yi güncelle
                await orderRepository.UpdateAsync(order, ct);
                successCount++;
                _logger.LogInformation(
                    "Sipariş expire edildi. OrderId: {OrderId}", order.OrderId);
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(ex,
                    "Sipariş expire edilemedi! OrderId: {OrderId}. Dead Letter Queue'ya taşınıyor.",
                    order.OrderId);
                // Dead Letter Queue'ya taşı
                await deadLetterService.EnqueueAsync(order, ex.Message, ct);
            }


        }

        _logger.LogInformation(
           "Expiration döngüsü tamamlandı. Başarılı: {Success}, Başarısız: {Failed}",
           successCount, failedCount);




    }
}
