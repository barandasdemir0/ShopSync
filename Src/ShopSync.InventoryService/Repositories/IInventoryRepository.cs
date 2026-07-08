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
    Task<List<InventoryTransactionLog>> GetExpiredReservationLogsAsync(DateTime expirationThreshold, DateTime lastProcessedThreshold, CancellationToken ct = default);


    //tüm stok bilgilerini getirir
    Task<List<InventoryItem>> GetAllItemsAsync(CancellationToken ct = default);

    // Belirli bir siparişin tamamlanıp tamamlanmadığını kontrol eder
    Task<bool> IsOrderAlreadyCompletedAsync(string orderId, CancellationToken ct = default);


    // Expiration job'ının son checkpoint değerini getirir.
    // Eğer daha önce hiç checkpoint kaydedilmemişse null döner.
    Task<ExpirationCheckpoint?> GetCheckpointAsync(string jobName, CancellationToken ct = default);

    // Expiration job'ının checkpoint değerini günceller veya yeni oluşturur.
    Task SaveCheckpointAsync(string jobName, DateTime lastProcessedThreshold, CancellationToken ct = default);


    // Belirli bir SKU ve depo koduna göre stok bilgisini getirir
    Task<InventoryItem?> GetBySkuAndWarehouseAsync(string sku, string warehouseCode, CancellationToken ct = default);

    // Belirli bir SKU'ya sahip tüm depolardaki stok bilgilerini getirir
    Task<List<InventoryItem>> GetBySkuAllWarehousesAsync(string sku, CancellationToken ct = default);


    // Yeni bir snapshot kaydeder.
    Task InsertSnapshotAsync(InventorySnapshot snapshot, CancellationToken ct = default);
    // Belirli bir snapshot'ı getirir.

    // snapshotId ile snapshot'ı getirir, eğer bulunamazsa null döner.
    Task<InventorySnapshot?> GetSnapshotByIdAsync(string snapshotId, CancellationToken ct = default);

    // Belirli bir SKU için, belirli bir tarihten itibaren yapılan tüm transaction loglarını getirir.
    Task<List<InventoryTransactionLog>> GetTransactionLogsForSkuAsync(string sku, DateTime since, CancellationToken ct = default);


}


//IClientSessionHandle, bir batch rezervasyonda stok güncelleme ve transaction log yazma işlemlerinin hep birlikte başarılı olması ya da hata olursa hep birlikte geri alınması için kullanılan MongoDB transaction oturumudur.
