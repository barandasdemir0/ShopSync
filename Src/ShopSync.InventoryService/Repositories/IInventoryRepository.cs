using MongoDB.Driver;
using ShopSync.InventoryService.Models;

namespace ShopSync.InventoryService.Repositories;

public interface IInventoryRepository
{
    // belirli stok bilgisini getirir
    Task<InventoryItem?> GetBySkuAsync(string sku, CancellationToken ct = default);

    //birden fazla stok bilgisini tek seferde getirir
    Task<List<InventoryItem>> GetBySkusAsync(IEnumerable<string> skus, CancellationToken ct = default);

    //stok bilgisini ekler
    Task InsertAsync(InventoryItem item, IClientSessionHandle? session = null, CancellationToken ct = default);

    //stok bilgisini günceller ıclient session handle ile transaction yönetimi sağlar
    Task UpdateAsync(InventoryItem item,IClientSessionHandle? session = null,CancellationToken ct = default);

    //stok bilgisini ekler ıclient session handle ile transaction yönetimi sağlar
    Task AddTransactionLogAsync( InventoryTransactionLog log, IClientSessionHandle? session = null, CancellationToken ct = default);

  

    //low stock threshold altındaki stokları getirir
    Task<List<InventoryItem>> GetLowStockItemsAsync(CancellationToken ct = default);

    // Belirtilen expirationThreshold tarihinden önce oluşturulmuş, süresi dolmuş rezervasyon transaction loglarını getirir.
    Task<List<InventoryTransactionLog>> GetExpiredReservationLogsAsync(DateTime expirationThreshold,CancellationToken ct = default);

    //tüm stok bilgilerini getirir
    Task<List<InventoryItem>> GetAllItemsAsync(CancellationToken ct = default);

}


//IClientSessionHandle, bir batch rezervasyonda stok güncelleme ve transaction log yazma işlemlerinin hep birlikte başarılı olması ya da hata olursa hep birlikte geri alınması için kullanılan MongoDB transaction oturumudur.
