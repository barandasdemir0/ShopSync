using Microsoft.Extensions.Options;
using ShopSync.InventoryService.Configuration;
using ShopSync.InventoryService.Repositories;

namespace ShopSync.InventoryService.BackgroundJobs;

public sealed class StockReconciliationJob : BackgroundService
{

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StockReconciliationJob> _logger;
    private readonly ReconciliationJobSettings _settings;

    public StockReconciliationJob(IServiceScopeFactory scopeFactory, ILogger<StockReconciliationJob> logger, IOptions< ReconciliationJobSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        _logger.LogInformation(
           "StockReconciliationJob başlatıldı. Kontrol aralığı: {Interval} dk",
           _settings.IntervalMinutes);


        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunReconciliationAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(
                    "StockReconciliationJob iptal edildi.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "StockReconciliationJob çalışırken hata oluştu.");
            }
            await Task.Delay(
                TimeSpan.FromMinutes(_settings.IntervalMinutes),
                stoppingToken);
        }
    }

    private async Task RunReconciliationAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IInventoryRepository>();


        _logger.LogDebug("Stok tutarlılık kontrolü başlatılıyor...");

        // Tüm ürünleri çek
        var allItems = await repository.GetAllItemsAsync(ct);
        
        var inconsistencyCount = 0;

        foreach (var item in allItems)
        {
            var issues = new List<string>();

            // Kontrol 1: QuantityAvailable negatif mi?
            if (item.QuantityAvailable < 0)
            {
                issues.Add(
                    $"QuantityAvailable NEGATİF: {item.QuantityAvailable}");
            }
            // Kontrol 2: QuantityReserved negatif mi?
            if (item.QuantityReserved < 0)
            {
                issues.Add(
                    $"QuantityReserved NEGATİF: {item.QuantityReserved}");
            }

            // Sorun varsa logla
            if (issues.Count > 0)
            {
                inconsistencyCount++;
                _logger.LogCritical(
                    "STOK TUTARSIZLIĞI TESPİT EDİLDİ! " +
                    "SKU: {Sku}, Warehouse: {Warehouse}, " +
                    "Available: {Available}, Reserved: {Reserved}, " +
                    "Sorunlar: {Issues}",
                    item.Sku,
                    item.WarehouseCode,
                    item.QuantityAvailable,
                    item.QuantityReserved,
                    string.Join(" | ", issues));
            }


        }

        if (inconsistencyCount == 0)
        {
            _logger.LogInformation(
                "Stok tutarlılık kontrolü tamamlandı. " +
                "Toplam {Count} ürün kontrol edildi. Sorun bulunamadı.",
                allItems.Count);
        }
        else
        {
            _logger.LogCritical(
                "Stok tutarlılık kontrolü tamamlandı. " +
                "{Total} üründen {Inconsistent} tanesinde TUTARSIZLIK tespit edildi!",
                allItems.Count, inconsistencyCount);
        }






    }







}
// ============================================================
// StockReconciliationJob.cs
// Stok Tutarlılık Kontrolü (Reconciliation)
//
// NEDEN GEREKLİ?
// Dağıtık sistemlerde şu senaryolar tutarsızlık yaratabilir:
// 1. Transaction commit edildi ama log yazılamadan uygulama çöktü
// 2. Ağ hatası nedeniyle MongoDB yarım güncelleme yaptı
// 3. Bir bug nedeniyle Available + Reserved denklemi bozuldu
//
// BU JOB NE YAPAR?
// 1. Tüm inventory_items collection'ını tarar
// 2. Her ürün için QuantityAvailable ve QuantityReserved'ın
//    negatif olmadığını kontrol eder
// 3. Sorun bulursa ALARM seviyesinde log yazar (düzeltme YAPMAZ!)
//
// NEDEN OTOMATİK DÜZELTMİYOR?
// Otomatik düzeltme tehlikelidir. Tutarsızlık tespit edildiğinde
// bir insan müdahale edip "Doğru değer ne olmalı?" diye bakmalıdır.
// Bu job sadece "Hey, burada bir sorun var!" diyen bir alarm sistemidir.
// ============================================================