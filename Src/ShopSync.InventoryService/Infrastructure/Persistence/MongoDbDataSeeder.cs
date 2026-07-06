using MongoDB.Driver;
using ShopSync.InventoryService.Models;

namespace ShopSync.InventoryService.Infrastructure.Persistence;

public sealed class MongoDbDataSeeder:BackgroundService
{
    private readonly MongoDbContext _dbContext;
    private readonly ILogger<MongoDbDataSeeder> _logger;
    public MongoDbDataSeeder(MongoDbContext dbContext, ILogger<MongoDbDataSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kodların başlatılabilmesi için ufak bir gecikme veriyoruz (Önce indexler oluşsun)
        await Task.Delay(2000, stoppingToken);
        _logger.LogInformation("Veritabanı veri kontrolü başlatılıyor...");
        try
        {
            // Veritabanında ürün var mı diye kontrol et
            var count = await _dbContext.InventoryItems.CountDocumentsAsync(
                FilterDefinition<InventoryItem>.Empty,
                cancellationToken: stoppingToken);

            if (count == 0)
            {
                _logger.LogInformation("Veritabanı boş, otomatik örnek (seed) veriler ekleniyor...");
                var warehouses = new[] { "DEFAULT", "WH-IST", "WH-ANK", "WH-IZM" };
                var random = new Random();
                var documents = new List<InventoryItem>();
                for (int i = 1; i <= 50; i++)
                {
                    string sku = $"SKU-{i:D3}"; // SKU-001, SKU-002 şeklinde oluşturur
                    foreach (var warehouse in warehouses)
                    {
                        int qty = random.Next(10, 501); // 10 ile 500 arası stok
                        // Kendi projendeki InventoryItem yapısı
                        var item = new InventoryItem(
                            sku: sku,
                            quantityAvailable: qty,
                            warehouseCode: warehouse,
                            lowStockThreshold: 20
                        );
                        documents.Add(item);
                    }
                }
                await _dbContext.InventoryItems.InsertManyAsync(documents, cancellationToken: stoppingToken);
                _logger.LogInformation($"Başarıyla eklendi! Toplam eklenen stok satırı sayısı: {documents.Count}");
            }
            else
            {
                _logger.LogInformation($"Veritabanında halihazırda {count} adet ürün var. Seed işlemi atlandı.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Otomatik veri ekleme (seed) sırasında bir hata oluştu.");
        }
    }
}
