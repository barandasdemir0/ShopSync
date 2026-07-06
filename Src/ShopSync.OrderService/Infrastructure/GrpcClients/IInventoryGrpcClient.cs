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
}
