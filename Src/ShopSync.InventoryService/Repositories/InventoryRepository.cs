using MongoDB.Driver;
using ShopSync.InventoryService.Infrastructure.Persistence;
using ShopSync.InventoryService.Models;

namespace ShopSync.InventoryService.Repositories;

public sealed class InventoryRepository : IInventoryRepository
{

    private readonly MongoDbContext _context;
    public InventoryRepository(MongoDbContext context)
    {
        _context = context;
    }


    //bir envanter işlemi gerçekleştiğinde, bu işlemin kaydını tutmak için bir işlem günlüğü ekler.
    public async Task AddTransactionLogAsync(InventoryTransactionLog log, IClientSessionHandle? session = null, CancellationToken ct = default)
    {

        // MongoDB'de bir işlem günlüğü ekle.
        if (log is null)
        {
            throw new ArgumentNullException(nameof(log));
        }

        // Eğer bir oturum (session) sağlanmışsa, oturum ile birlikte ekleme işlemi yap. Aksi takdirde normal ekleme işlemi yap.
        if (session != null)
        {
            // Oturum ile birlikte ekleme işlemi yap.
            await _context.TransactionLogs.InsertOneAsync(session, log, cancellationToken: ct);
        }

        else
        {
            // Normal ekleme işlemi yap.
            await _context.TransactionLogs.InsertOneAsync(log, cancellationToken: ct);
        }
    }


    // SKU'ya göre envanter öğesini getir.
    public async Task<InventoryItem?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU boş bırakılamaz.", nameof(sku));
        }


        var normalizedSku = sku.Trim().ToUpperInvariant();

        // SKU'ya göre envanter öğesini getir.
        return await _context.InventoryItems
            .Find(x => x.Sku == normalizedSku)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<InventoryItem>> GetBySkusAsync(IEnumerable<string> skus, CancellationToken ct = default)
    {
        if (skus is null)
        {
            throw new ArgumentNullException(nameof(skus));
        }
        // Birden fazla SKU'yu tek sorguda getir.
        var normalizedSkus = skus
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim().ToUpperInvariant())
        .Distinct() //distinct() ile aynı SKU'ları filtrele
        .ToList();

        // Eğer filtrelenmiş SKU listesi boşsa, boş bir liste döndür.
        if (normalizedSkus.Count == 0)
        {
            return new List<InventoryItem>();
        }

        // MongoDB'de SKU'ya göre envanter öğelerini getir.
        var filter = Builders<InventoryItem>.Filter.In(x => x.Sku, normalizedSkus);

        return await _context.InventoryItems
            .Find(filter)
            .ToListAsync(ct);
    }

    // Belirtilen expirationThreshold tarihinden önce oluşturulmuş, süresi dolmuş rezervasyon transaction loglarını getirir.
    public async Task<List<InventoryTransactionLog>> GetExpiredReservationLogsAsync(DateTime expirationThreshold, CancellationToken ct = default)
    {

        // MongoDB'de süresi dolmuş rezervasyon transaction loglarını getir.
        var reserveLogs = await FetchExpiredReserveLogsAsync(expirationThreshold, ct);

        // Eğer süresi dolmuş rezervasyon transaction logları yoksa, boş bir liste döndür.
        if (reserveLogs.Count == 0)
        {
            return new List<InventoryTransactionLog>();
        }
        // Süresi dolmuş rezervasyon transaction loglarından, OrderId'si olanları filtrele ve distinct olarak al.
        var orderIds = reserveLogs
            .Where(x => !string.IsNullOrWhiteSpace(x.OrderId))
            .Select(x => x.OrderId!)
            .Distinct()
            .ToList();

        if (orderIds.Count == 0)
        {
            return new List<InventoryTransactionLog>();
        }

        // Belirli bir sipariş ID listesi için tamamlanmış transaction loglarını getir.
        var completedOrderIds = await FetchCompletedOrderIdsAsync(orderIds, ct);


        // Süresi dolmuş rezervasyon transaction loglarından, tamamlanmamış olanları filtrele ve liste olarak döndür.
        return reserveLogs
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.OrderId) &&
                !completedOrderIds.Contains(x.OrderId!))
            .ToList();


    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync(CancellationToken ct = default)
    {
        // MongoDB'de düşük stok seviyesindeki envanter öğelerini getir.
        var filter = Builders<InventoryItem>.Filter.Where(
         x => x.QuantityAvailable <= x.LowStockThreshold);

        // Düşük stok seviyesindeki envanter öğelerini liste olarak döndür.
        return await _context.InventoryItems.Find(filter).ToListAsync(ct);
    }


    public async Task InsertAsync(InventoryItem item, IClientSessionHandle? session = null, CancellationToken ct = default)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }
        if (session != null)
        {
            await _context.InventoryItems.InsertOneAsync(
                session,
                item,
                cancellationToken: ct);
        }
        else
        {
            await _context.InventoryItems.InsertOneAsync(
                item,
                cancellationToken: ct);
        }
    }

    public async Task UpdateAsync(InventoryItem item, IClientSessionHandle? session = null, CancellationToken ct = default)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        // MongoDB'de envanter öğesini güncelle.
        var filter = Builders<InventoryItem>.Filter.Eq(x => x.Id, item.Id);
        if (session != null)
        {
            await _context.InventoryItems.ReplaceOneAsync(session, filter, item, cancellationToken: ct);
        }

        else
        {
            await _context.InventoryItems.ReplaceOneAsync(filter, item, cancellationToken: ct);
        }
    }

    // Bu metod, belirli bir tarihten önce oluşturulmuş ve süresi dolmuş rezervasyon transaction loglarını getirir.
    private async Task<List<InventoryTransactionLog>> FetchExpiredReserveLogsAsync(DateTime expirationThreshold, CancellationToken ct)
    {
        return await _context.TransactionLogs
           .Find(x =>
               x.TransactionType == InventoryTransactionType.Reserve.Code &&
               x.Timestamp < expirationThreshold)
           .ToListAsync(ct);
    }

    // Belirli sipariş ID'leri için CONFIRM, RELEASE veya EXPIRATION işlemi olan OrderId'leri getirir.
    private async Task<HashSet<string>> FetchCompletedOrderIdsAsync(List<string> orderIds, CancellationToken ct)
    {
        var completedTypes = new[]
   {
        InventoryTransactionType.Confirm.Code,
        InventoryTransactionType.Release.Code,
        InventoryTransactionType.Expiration.Code
    };

        var completedList = await _context.TransactionLogs
            .Find(x =>
                x.OrderId != null &&
                orderIds.Contains(x.OrderId) &&
                completedTypes.Contains(x.TransactionType))
            .Project(x => x.OrderId)
            .ToListAsync(ct);

        return completedList
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToHashSet();
    }

    public async Task<List<InventoryItem>> GetAllItemsAsync(CancellationToken ct = default)
    {
        // MongoDB'deki tüm envanter öğelerini getir. filtre yok
        return await _context.InventoryItems
        .Find(Builders<InventoryItem>.Filter.Empty).ToListAsync(ct);
    }

    public async Task<bool> IsOrderAlreadyCompletedAsync(string orderId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return false;
        }

        // Tamamlanmış sayılan işlem tipleri
        var completedTypes = new[]
        {
            InventoryTransactionType.Confirm.Code,
            InventoryTransactionType.Release.Code,
            InventoryTransactionType.Expiration.Code
        };

        //  sipariş ID'si için tamamlanmış transaction loglarının sayısını al.
        var count = await _context.TransactionLogs
            .CountDocumentsAsync(
           x => x.OrderId == orderId && completedTypes.Contains(x.TransactionType),
           cancellationToken: ct);
        return count > 0;

    }

    public async Task<ExpirationCheckpoint?> GetCheckpointAsync(string jobName, CancellationToken ct = default)
    {
        return await _context.ExpirationCheckpoints
            .Find(x => x.JobName == jobName)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveCheckpointAsync(string jobName, DateTime lastProcessedThreshold, CancellationToken ct = default)
    {
        // Mevcut checkpoint'i bul
        var existing = await GetCheckpointAsync(jobName, ct);
        if (existing is null)
        {
            //  Yeni checkpoint oluştur
            var checkpoint = new ExpirationCheckpoint(jobName, lastProcessedThreshold);
            await _context.ExpirationCheckpoints.InsertOneAsync(checkpoint, cancellationToken: ct);
        }
        else
        {
            //  DDD metodu ile güncelle ve MongoDB'ye yaz
            existing.Update(lastProcessedThreshold);
            //filter ile Id'si eşleşen kaydı bul ve güncelle
            var filter = Builders<ExpirationCheckpoint>.Filter.Eq(x => x.Id, existing.Id);
            await _context.ExpirationCheckpoints.ReplaceOneAsync(filter, existing, cancellationToken: ct);
        }
    }

    public async Task<InventoryItem?> GetBySkuAndWarehouseAsync(string sku, string warehouseCode, CancellationToken ct = default)
    {
        var normalizedSku = sku.Trim().ToUpperInvariant();
        var normalizedWarehouse = warehouseCode.Trim().ToUpperInvariant();
        return await _context.InventoryItems
            .Find(x => x.Sku == normalizedSku && x.WarehouseCode == normalizedWarehouse)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<InventoryItem>> GetBySkuAllWarehousesAsync(string sku, CancellationToken ct = default)
    {
        var normalizedSku = sku.Trim().ToUpperInvariant();
        return await _context.InventoryItems
            .Find(x => x.Sku == normalizedSku)
            .SortByDescending(x => x.QuantityAvailable)
            .ToListAsync(ct);
    }

    public async Task InsertSnapshotAsync(InventorySnapshot snapshot, CancellationToken ct = default)
    {
        await _context.Snapshots.InsertOneAsync(snapshot, cancellationToken: ct);
    }

    public async Task<InventorySnapshot?> GetSnapshotByIdAsync(string snapshotId, CancellationToken ct = default)
    {
        return await _context.Snapshots
        .Find(x => x.Id == snapshotId)
        .FirstOrDefaultAsync(ct);
    }
}
