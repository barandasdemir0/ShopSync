using Microsoft.Extensions.Options;
using ShopSync.InventoryService.Configuration;
using ShopSync.InventoryService.Repositories;

namespace ShopSync.InventoryService.BackgroundJobs;

public sealed class LowStockAlertJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LowStockAlertJob> _logger;
    private readonly LowStockAlertSettings _settings;
    public LowStockAlertJob(
        IServiceScopeFactory scopeFactory,
        ILogger<LowStockAlertJob> logger,
        IOptions<LowStockAlertSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
          "LowStockAlertJob başlatıldı. Kontrol aralığı: {Interval} dk",
          _settings.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckLowStockAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(
                    "LowStockAlertJob iptal edildi.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "LowStockAlertJob çalışırken hata oluştu.");
            }
            await Task.Delay(
                TimeSpan.FromMinutes(_settings.IntervalMinutes),
                stoppingToken);
        }


    }


    private async Task CheckLowStockAsync(CancellationToken ct)
    {

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<IInventoryRepository>();
        _logger.LogDebug("Düşük stok kontrolü başlatılıyor...");

        var lowStockItems = await repository.GetLowStockItemsAsync(ct);



        if (lowStockItems.Count == 0)
        {
            _logger.LogDebug("Düşük stoklu ürün bulunamadı.");
            return;
        }

        foreach (var item in lowStockItems)
        {
            _logger.LogWarning(
                "DÜŞÜK STOK UYARISI! " +
                "SKU: {Sku}, Depo: {Warehouse}, " +
                "Mevcut: {Available}, Eşik: {Threshold}, " +
                "Rezerve: {Reserved}",
                item.Sku,
                item.WarehouseCode,
                item.QuantityAvailable,
                item.LowStockThreshold,
                item.QuantityReserved);
        }
        _logger.LogWarning(
            "Toplam {Count} üründe düşük stok uyarısı var!",
            lowStockItems.Count);

    }







}





// ============================================================
// LowStockAlertJob.cs
// Düşük Stok Uyarı Sistemi
//
// NEDEN GEREKLİ?
// Bir ürünün stoku kritik seviyeye düştüğünde operasyon ekibinin
// haberdar olması gerekir. Aksi halde ürün tükenir ve satış kaybedilir.
//
// BU JOB NE YAPAR?
// 1. Her X dakikada bir (varsayılan 5 dk) uyanır
// 2. Tüm ürünleri tarar
// 3. QuantityAvailable <= LowStockThreshold olan ürünleri bulur
//    (her ürünün kendi eşik değeri var, varsayılan 10)
// 4. Bu ürünleri WARNING seviyesinde loglar
//
// İLERİDE: Bu loglar Grafana üzerinden alert rule'a bağlanabilir
// ve Slack/Email bildirimi gönderilebilir.
// ============================================================