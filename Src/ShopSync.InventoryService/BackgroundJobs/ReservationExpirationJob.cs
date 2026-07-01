using Microsoft.Extensions.Options;
using ShopSync.InventoryService.Configuration;
using ShopSync.InventoryService.Infrastructure.Locking;
using ShopSync.InventoryService.Infrastructure.Persistence;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Repositories;

namespace ShopSync.InventoryService.BackgroundJobs;

public sealed class ReservationExpirationJob : BackgroundService
{
  
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpirationJob> _logger;
    private readonly ExpirationJobSettings _settings;

    public ReservationExpirationJob(IServiceScopeFactory scopeFactory, ILogger<ReservationExpirationJob> logger, IOptions<ExpirationJobSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }


    // Bu metot, belirli aralıklarla süresi dolmuş rezervasyonları kontrol eder ve serbest bırakır.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
          "ReservationExpirationJob başlatıldı. " +
          "Kontrol aralığı: {Interval} dk, Süre sınırı: {Expiry} dk",
          _settings.IntervalMinutes, _settings.ExpirationMinutes);

        // Uygulama kapanana kadar sürekli dönen ana döngü
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Süresi dolmuş rezervasyonları kontrol et ve serbest bırak
                await ProcessExpiredReservationsAsync(stoppingToken);
            }
            // Eğer uygulama kapanıyorsa, iptal isteği geldiğinde döngüyü kır
            catch (OperationCanceledException)
            {
                // Uygulama kapanıyor, normal bir durum
                _logger.LogInformation("ReservationExpirationJob iptal edildi .");
                break;
            }
            catch (Exception ex)
            {
                // Job çöktüğünde uygulamayı değil, sadece bu döngüyü durdur
                _logger.LogError(ex,
                    "ReservationExpirationJob çalışırken hata oluştu. " +
                    "Sonraki denemede tekrar denenecek.");
            }
            // Bir sonraki kontrol zamanına kadar bekle
            await Task.Delay(
                TimeSpan.FromMinutes(_settings.IntervalMinutes),
                stoppingToken);
        }


    }

    // Bu metot, süresi dolmuş rezervasyonları bulur ve her birini serbest bırakır.
    private async Task ProcessExpiredReservationsAsync(CancellationToken ct)
    {
        // Yeni bir scope oluşturuluyor, böylece her işlem için bağımlılıklar yeniden çözülüyor
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();

       
        // Rezervasyonların geçerli kalacağı süre.
        // Örneğin: 10 dakika
        var reservationLifetime = TimeSpan.FromMinutes(_settings.ExpirationMinutes);

        // Bu tarihten daha eski rezervasyonlar süresi dolmuş kabul edilir.
        // Örneğin şu an 12:30 ise ve süre 10 dakikaysa,
        // 12:20'den önceki rezervasyonlar expired kabul edilir.
        var expirationThreshold = DateTime.UtcNow - reservationLifetime;

        _logger.LogDebug(
       "Süresi dolmuş rezervasyonlar taranıyor. Eşik: {Threshold}",
       expirationThreshold);

        // Süresi dolmuş RESERVE loglarını bul
        var expiredLogs = await repository.GetExpiredReservationLogsAsync(
            expirationThreshold, ct);
        if (expiredLogs.Count == 0)
        {
            _logger.LogDebug("Süresi dolmuş rezervasyon bulunamadı.");
            return;
        }

        _logger.LogInformation(
            "Süresi dolmuş {Count} adet rezervasyon bulundu. İşleniyor...",
            expiredLogs.Count);

        // Süresi dolmuş logları OrderId bazında grupla
        // Bir siparişin birden fazla SKU'su olabilir, hepsini birlikte işlenmeli
        var groupedByOrder = expiredLogs
            .GroupBy(l => l.OrderId)
            .ToList();


        foreach (var orderGroup in groupedByOrder)
        {
            try
            {
                await ReleaseExpiredOrderAsync(
                    orderGroup.Key!,
                    orderGroup.ToList(),
                    repository,
                    dbContext,
                    lockService,
                    ct);
            }
            catch (Exception ex)
            {
                // Bir siparişin release'i başarısız olursa diğerlerini etkilememeli
                _logger.LogError(ex,
                    "Süresi dolmuş sipariş release edilirken hata. OrderId: {OrderId}",
                    orderGroup.Key);
            }
        }

    }


    private async Task ReleaseExpiredOrderAsync(string orderId,List<InventoryTransactionLog> reserveLogs,IInventoryRepository repository,MongoDbContext dbContext,IDistributedLockService lockService,CancellationToken ct)
    {

        // OrderId bazında gruplanmış logları kullanarak SKU bazında toplam miktarı hesapla
        var itemsToRelease = reserveLogs
           .GroupBy(l => l.Sku.Trim().ToUpperInvariant())
           .Select(g => new
           {
               NormalizedSku = g.Key,
               TotalQuantity = g.Sum(l => l.Quantity)
           })
           .ToList();


        var skus = itemsToRelease.Select(i => i.NormalizedSku).ToList();

        _logger.LogInformation(
          "Süresi dolmuş sipariş release ediliyor. OrderId: {OrderId}, SKU sayısı: {Count}",
          orderId, skus.Count);


        // SKU'ları alfabetik sırayla kilitle
        await using var lockHandle = await lockService.AcquireLocksAsync(skus, cancellationToken: ct);

        // Kilitler alındıktan sonra, SKU'ları veritabanından çek
        var inventoryItems = await repository.GetBySkusAsync(skus, ct);
        var inventoryMap = inventoryItems
            .ToDictionary(i => i.Sku.Trim().ToUpperInvariant()); // SKU'ları normalize ederek dictionary oluştur

        // MongoDB Transaction başlat
        using var session = await dbContext.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();

        try
        {
            foreach (var item in itemsToRelease)
            {
                // SKU veritabanında bulunamazsa atla 
                if (!inventoryMap.ContainsKey(item.NormalizedSku))
                {
                    _logger.LogWarning(
                        "Expiration: SKU bulunamadı, atlanıyor. SKU: {Sku}",
                        item.NormalizedSku);
                    continue;
                }

                var stock = inventoryMap[item.NormalizedSku];
                var prevAvailable = stock.QuantityAvailable;
                var prevReserved = stock.QuantityReserved;

                var releaseQuantity = Math.Min(item.TotalQuantity, stock.QuantityReserved);

                if (releaseQuantity <= 0)
                {
                    _logger.LogDebug(
                        "Expiration: Zaten serbest bırakılmış, atlanıyor. SKU: {Sku}",
                        item.NormalizedSku);
                    continue;
                }

                stock.Release(releaseQuantity);



                await repository.UpdateAsync(stock, session, ct);

                var log = new InventoryTransactionLog(
                    sku: item.NormalizedSku,
                    transactionType: InventoryTransactionType.Expiration,
                    quantity: releaseQuantity,
                    previousAvailable: prevAvailable,
                    newAvailable: stock.QuantityAvailable,
                    previousReserved: prevReserved,
                    newReserved: stock.QuantityReserved,
                    orderId: orderId,
                    reason: $"Rezervasyon süresi doldu ({_settings.ExpirationMinutes} dk)");
                await repository.AddTransactionLogAsync(log, session, ct);

            }

            await session.CommitTransactionAsync(ct);
            _logger.LogInformation(
                "Süresi dolmuş sipariş başarıyla release edildi. OrderId: {OrderId}",
                orderId);
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync(ct);
            _logger.LogError(ex,
                "Expiration transaction hatası. OrderId: {OrderId}", orderId);
            throw;
        }


    }

}
