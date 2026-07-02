using MongoDB.Driver;
using ShopSync.OrderService.Models;

namespace ShopSync.OrderService.Infrastructure.Persistence;

public sealed class MongoDbIndexInitializer : BackgroundService
{

    private readonly MongoDbContext _dbContext;
    private readonly ILogger<MongoDbIndexInitializer> _logger;
    public MongoDbIndexInitializer(MongoDbContext dbContext, ILogger<MongoDbIndexInitializer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MongoDB index oluşturma başlatılıyor...");
        try
        {
            await CreateOrderIndexesAsync(stoppingToken);
            _logger.LogInformation("Tüm MongoDB indexleri başarıyla oluşturuldu.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB index oluşturma sırasında hata oluştu.");
        }
    }

    private async Task CreateOrderIndexesAsync(CancellationToken ct)
    {
        var collection = _dbContext.Orders;

        // Index 1: OrderId (Benzersiz - Unique)
        // Aynı OrderId ile iki sipariş oluşmasını engeller.
        // Ayrıca GetByOrderIdAsync sorgusunu hızlandırır.
        var orderIdIndex = new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Ascending(x => x.OrderId),
            new CreateIndexOptions 
            { 
                Unique = true,
                Name = "idx_orderid_unique" 
            });

        // Index 2: Status (Tekli)
        // Duruma göre filtreleme sorgularını hızlandırır.
        // Örn: "Tüm PENDING siparişleri getir"
        var statusIndex = new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Ascending(x => x.Status),
            new CreateIndexOptions
            { 
                Name = "idx_status" 
            });

        // Index 3: CreatedAt (Tekli, Azalan)
        // Tarih aralığına göre filtreleme ve sıralama için.
        // En yeni siparişler önce gelsin → Descending
        var createdAtIndex = new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Descending(x => x.CreatedAt),
            new CreateIndexOptions 
            { 
                Name = "idx_created_at_desc" 
            });

        // Index 4: IdempotencyKey (Sparse Unique)
        // Idempotency kontrolü için. Sparse: null olan kayıtları indexlemez.
        var idempotencyIndex = new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys.Ascending(x => x.IdempotencyKey),
            new CreateIndexOptions
            {
                Unique = true,
                Sparse = true,
                Name = "idx_idempotencykey_unique_sparse"
            });


        // Index 5: Status + CreatedAt (Compound)
        // "Son 24 saatteki PENDING siparişleri getir" gibi
        // hem duruma hem tarihe göre filtreleyen sorguları hızlandırır.
        var statusCreatedIndex = new CreateIndexModel<Order>(
            Builders<Order>.IndexKeys
                .Ascending(x => x.Status)
                .Descending(x => x.CreatedAt),
            new CreateIndexOptions { Name = "idx_status_created" });
        await collection.Indexes.CreateManyAsync(
            new[] 
            { 
                orderIdIndex, 
                statusIndex, 
                createdAtIndex,
                idempotencyIndex, 
                statusCreatedIndex },
            ct);
        _logger.LogDebug("Orders indexleri oluşturuldu.");


    }
}
