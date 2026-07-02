using MongoDB.Driver;
using ShopSync.InventoryService.Models;

namespace ShopSync.InventoryService.Infrastructure.Persistence;

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
            await CreateInventoryItemIndexesAsync(stoppingToken);
            await CreateTransactionLogIndexesAsync(stoppingToken);
            await CreateCheckpointIndexesAsync(stoppingToken);
            _logger.LogInformation("Tüm MongoDB indexleri başarıyla oluşturuldu.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB index oluşturma sırasında hata oluştu.");
        }
    }


    // Checkpoint koleksiyonu için index oluşturma
    private async Task CreateInventoryItemIndexesAsync(CancellationToken ct)
    {
        // Index 1: SKU + WarehouseCode (Tekil)
        var collection = _dbContext.InventoryItems;

        // Bu index, SKU ve WarehouseCode kombinasyonunun tekil olmasını sağlar.
        var skuWarehouseIndex = new CreateIndexModel<InventoryItem>(
            Builders<InventoryItem>
            .IndexKeys // SKU ve WarehouseCode alanlarına göre artan sırada index oluşturur.
                .Ascending(x => x.Sku) // SKU alanına göre artan sırada index oluşturur.
                .Ascending(x => x.WarehouseCode), // WarehouseCode alanına göre artan sırada index oluşturur.
            new CreateIndexOptions  // Indexin tekil olmasını sağlar ve indexin adını belirler.
            {
                Unique = true,  // Indexin tekil olmasını sağlar.
                Name = "idx_sku_warehouse_unique"  // Indexin adını belirler.
            });



        // Index 2: SKU (Tekli)
        // GetBySkuAsync ve GetBySkusAsync sorgularını hızlandırır.
        var skuIndex = new CreateIndexModel<InventoryItem>(
            Builders<InventoryItem> // IndexKeys özelliği, indexin hangi alanlara göre oluşturulacağını belirtir.
            .IndexKeys.Ascending(x => x.Sku), // SKU alanına göre artan sırada index oluşturur.
                new CreateIndexOptions
                {
                    Name = "idx_sku" // Indexin adını belirler.
                });


        // Index 3: QuantityAvailable (LowStock sorgusu için)
        // GetLowStockItemsAsync sorgusunu hızlandırır.
        var lowStockIndex = new CreateIndexModel<InventoryItem>(
            Builders<InventoryItem>
            .IndexKeys.Ascending(x => x.QuantityAvailable), // QuantityAvailable alanına göre artan sırada index oluşturur.
            new CreateIndexOptions
            {
                Name = "idx_quantity_available"  // Indexin adını belirler.
            });



        // Indexleri oluştur
        await collection.Indexes.CreateManyAsync(
            new[]
            {
                skuWarehouseIndex,
                skuIndex,
                lowStockIndex },
            ct);
        _logger.LogDebug("InventoryItems indexleri oluşturuldu.");
    }



    // TransactionLog koleksiyonu için index oluşturma
    private async Task CreateTransactionLogIndexesAsync(CancellationToken ct)
    {
        var collection = _dbContext.TransactionLogs;

        // Index 1: OrderId + TransactionType
        // IsOrderAlreadyCompletedAsync ve FetchCompletedOrderIdsAsync sorgularını hızlandırır.
        var orderTypeIndex = new CreateIndexModel<InventoryTransactionLog>(
            Builders<InventoryTransactionLog>.IndexKeys
                .Ascending(x => x.OrderId) // OrderId alanına göre artan sırada index oluşturur.
                .Ascending(x => x.TransactionType), //  TransactionType alanına göre artan sırada index oluşturur.
            new CreateIndexOptions
            {
                Name = "idx_orderid_type" // Indexin adını belirler.
            });






        // Index 2: TransactionType + Timestamp (Compound)
        // FetchExpiredReserveLogsAsync sorgusunu hızlandırır.
        var typeTimestampIndex = new CreateIndexModel<InventoryTransactionLog>(
            Builders<InventoryTransactionLog>.IndexKeys
                .Ascending(x => x.TransactionType)
                .Ascending(x => x.Timestamp),
            new CreateIndexOptions
            {
                Name = "idx_type_timestamp"
            });



        // Index 3: SKU (Tekli)
        // SKU bazlı log sorgularını hızlandırır.
        var skuIndex = new CreateIndexModel<InventoryTransactionLog>(
            Builders<InventoryTransactionLog>
            .IndexKeys.Ascending(x => x.Sku),
            new CreateIndexOptions 
            { 
                Name = "idx_sku" 
            });





        await collection.Indexes.CreateManyAsync(
            new[]  //dizi oluşturulacak indexleri içerir
            { 
                orderTypeIndex,
                typeTimestampIndex, 
                skuIndex },
            ct);
        _logger.LogDebug("TransactionLogs indexleri oluşturuldu.");
    }


    // ExpirationCheckpoint koleksiyonu için index oluşturma
    private async Task CreateCheckpointIndexesAsync(CancellationToken ct)
    {
        // ExpirationCheckpoint koleksiyonu için index oluşturma
        var collection = _dbContext.ExpirationCheckpoints;


        // Index: JobName (Benzersiz)
        // Her job adı için tek bir checkpoint olmalı.
        var jobNameIndex = new CreateIndexModel<ExpirationCheckpoint>(
            Builders<ExpirationCheckpoint>
            .IndexKeys.Ascending(x => x.JobName),
            new CreateIndexOptions 
            {
                Unique = true, 
                Name = "idx_jobname_unique" 
            });


        await collection.Indexes.CreateManyAsync(
            new[] { jobNameIndex },
            ct);
        _logger.LogDebug("ExpirationCheckpoints indexleri oluşturuldu.");
    }




}
