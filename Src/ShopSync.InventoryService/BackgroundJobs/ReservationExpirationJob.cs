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


        var checkpoint = await repository.GetCheckpointAsync("ReservationExpirationJob", ct);

        var lastProcessedThreshold = checkpoint?.LastProcessedThreshold ?? DateTime.MinValue;

        var reservationLifetime = TimeSpan.FromMinutes(_settings.ExpirationMinutes);

      
        // Süresi dolmuş rezervasyonları getir
        var expirationThreshold = DateTime.UtcNow - reservationLifetime;

        // Süresi dolmuş rezervasyonları getir
        var expiredLogs = await repository.GetExpiredReservationLogsAsync(
            expirationThreshold,
            lastProcessedThreshold, // Checkpoint parametresi eklendi
            ct);
        if (checkpoint is not null)
        {
            _logger.LogInformation(
                "Checkpoint bulundu. Son başarılı tarama: {LastProcessed}",
                checkpoint.LastProcessedThreshold);
        }
        else
        {
            _logger.LogInformation(
                "Checkpoint bulunamadı. İlk tarama yapılacak.");
        }
        _logger.LogDebug(
           "Süresi dolmuş rezervasyonlar taranıyor. Eşik: {Threshold}",
           expirationThreshold);
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

        await repository.SaveCheckpointAsync("ReservationExpirationJob", expirationThreshold, ct);
        _logger.LogDebug("Checkpoint güncellendi: {Threshold}", expirationThreshold);

    }


    private async Task ReleaseExpiredOrderAsync(string orderId,List<InventoryTransactionLog> reserveLogs,IInventoryRepository repository,MongoDbContext dbContext,IDistributedLockService lockService,CancellationToken ct)
    {

        var alreadyCompleted = await repository.IsOrderAlreadyCompletedAsync(orderId, ct);
        if (alreadyCompleted)
        {
            _logger.LogDebug(
                "Expiration: Bu sipariş zaten işlenmiş, atlanıyor. OrderId: {OrderId}",
                orderId);
            return;
        }

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
        var inventoryItems = (await repository.GetBySkusAsync(skus, ct));
        var inventoryMap = inventoryItems
            .GroupBy(i => i.Sku.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.ToList()); // SKU'ları normalize ederek dictionary oluştur

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
                    _logger.LogWarning("Expiration: SKU bulunamadı, atlanıyor. SKU: {Sku}", item.NormalizedSku);
                    continue;
                }
                var stocksForSku = inventoryMap[item.NormalizedSku];
                var remainingToRelease = item.TotalQuantity;
                bool logInserted = false;
                // O SKU'ya ait depoları dön, hangisinde Reserved stok varsa ondan düş
                foreach (var stock in stocksForSku.Where(s => s.QuantityReserved > 0))
                {
                    if (remainingToRelease <= 0)
                    {
                        break; // İade edilecek miktar bittiyse çık
                    }
                    var prevAvailable = stock.QuantityAvailable;
                    var prevReserved = stock.QuantityReserved;
                    var releaseQuantity = Math.Min(remainingToRelease, stock.QuantityReserved);

                    stock.Release(releaseQuantity);
                    remainingToRelease -= releaseQuantity;
                    await repository.UpdateAsync(stock, session, ct);
                    if (releaseQuantity > 0)
                    {
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
                        logInserted = true;
                    }
                }
                // EĞER HİÇ LOG YAZILMADIYSA 
                if (!logInserted)
                {
                    // Stoklarda değişiklik yapmadan siparişin iptal edildiğini (tamamlandığını) DB'ye bildir
                    var defaultStock = stocksForSku.FirstOrDefault();
                    var prevAvail = defaultStock?.QuantityAvailable ?? 0;
                    var prevRes = defaultStock?.QuantityReserved ?? 0;

                    // Loga yazılacak miktarı (quantity) okunabilir bir if-else bloğu ile belirliyoruz
                    int logQuantity;
                    if (item.TotalQuantity > 0)
                    {
                        logQuantity = item.TotalQuantity;
                    }
                    else
                    {
                        // Veritabanı kuralları gereği log miktarı 0 olamaz, bu yüzden en az 1 yapıyoruz
                        logQuantity = 1;
                    }
                    var log = new InventoryTransactionLog(
                        sku: item.NormalizedSku,
                        transactionType: InventoryTransactionType.Expiration,
                        quantity: logQuantity,
                        previousAvailable: prevAvail,
                        newAvailable: prevAvail,
                        previousReserved: prevRes,
                        newReserved: prevRes,
                        orderId: orderId,
                        reason: $"Rezervasyon süresi doldu ({_settings.ExpirationMinutes} dk) - Stok zaten 0");
                    await repository.AddTransactionLogAsync(log, session, ct);
                }
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
