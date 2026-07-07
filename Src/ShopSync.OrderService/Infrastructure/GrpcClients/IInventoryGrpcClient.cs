using ShopSync.InventoryService.Protos;

namespace ShopSync.OrderService.Infrastructure.GrpcClients;

public interface IInventoryGrpcClient
{
    // Toplu stok rezervasyonu yap
    Task<ReserveBatchResponse> ReserveBatchAsync(
        string orderId,
        IEnumerable<ReservationItem> items,
        CancellationToken ct = default);


    // Toplu stok serbest bırakma
    Task<ReleaseBatchResponse> ReleaseBatchAsync(
        string orderId,
        IEnumerable<ReservationItem> items,
        CancellationToken ct = default);
    

    // Siparişin envanterini onayla (stoktan kalıcı olarak düş)
    Task<StockOperationResponse> ConfirmReservationAsync(string orderId, IEnumerable<ReservationItem> items, CancellationToken ct = default);

    // Inventory Snapshot (Yedek) oluştur
    Task<CreateSnapshotResponse> CreateSnapshotAsync(string description, CancellationToken ct = default);


    // Oluşturulan bir Snapshot'ı geri yükle
    Task<StockOperationResponse> RestoreSnapshotAsync(string snapshotId, CancellationToken ct = default);


    // Stok bilgisini getir (Mevcut ve Rezerve)
    Task<GetStockResponse> GetStockAsync(string sku, CancellationToken ct = default);


    // Stok artır
    Task<StockOperationResponse> IncreaseStockAsync(string sku, int quantity, string reason, string warehouseCode, CancellationToken ct = default);


    // Stok azalt
    Task<StockOperationResponse> DecreaseStockAsync(string sku, int quantity, string reason, string warehouseCode, CancellationToken ct = default);


    // Depolar arası ürün transferi yap
    Task<StockOperationResponse> RebalanceStockAsync(string sku, int quantity, string fromLocation, string toLocation, string reason, CancellationToken ct = default);


    // Yeni ürün/depo kaydı aç
    Task<StockOperationResponse> CreateInventoryItemAsync(string sku, int initialQuantity, string warehouseCode, int lowStockThreshold, CancellationToken ct = default);


    // Mevcut ürünü depodan sil
    Task<StockOperationResponse> DeleteInventoryItemAsync(string sku, string warehouseCode, CancellationToken ct = default);


}
