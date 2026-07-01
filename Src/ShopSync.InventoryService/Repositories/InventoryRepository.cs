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
}
