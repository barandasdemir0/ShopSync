using Refit;
using ShopSync.Web.Dtos;

namespace ShopSync.Web.Services;

public interface IShopSyncApi
{ 
    //order
    [Post("/api/Order")]
    Task<OrderResponseDto> CreateOrderAsync([Body] CreateOrderRequestDto request,
        CancellationToken cancellationToken = default);
  

    [Get("/api/Order/{orderId}")]
    Task<OrderResponseDto> GetOrderAsync(string orderId,
        CancellationToken cancellationToken = default);
    

    [Delete("/api/Order/{orderId}")]
    Task<OrderResponseDto> CancelOrderAsync(string orderId, [Body] CancelOrderRequestDto request,
        CancellationToken cancellationToken = default);
   

    [Post("/api/Order/{orderId}/confirm")]
    Task<OrderResponseDto> ConfirmOrderAsync(string orderId, [Body] ConfirmOrderRequestDto request,
        CancellationToken cancellationToken = default);

    [Get("/api/Order")]
    Task<PagedResponseDto<OrderResponseDto>> ListOrdersAsync([Query] OrderFilterDto filter, CancellationToken cancellationToken = default);

    [Post("/api/Order/batch-cancel")]
    Task<BatchCancelResponseDto> BatchCancelAsync([Body] BatchCancelRequestDto request, CancellationToken cancellationToken = default);

    [Post("/api/Order/{orderId}/admin-override")]
    Task<OrderResponseDto> AdminOverrideAsync(string orderId, [Body] AdminOverrideRequestDto request, CancellationToken cancellationToken = default);


    //envanter
    [Get("/api/Inventory/stock/{sku}")]
    Task<GetStockResponseDto> GetStockAsync(string sku, CancellationToken cancellationToken = default);
    [Post("/api/Inventory/stock/increase")]
    Task<StockOperationResponseDto> IncreaseStockAsync([Body] IncreaseStockDto request, CancellationToken cancellationToken = default);
    [Post("/api/Inventory/stock/decrease")]
    Task<StockOperationResponseDto> DecreaseStockAsync([Body] DecreaseStockDto request, CancellationToken cancellationToken = default);
    [Post("/api/Inventory/stock/rebalance")]
    Task<StockOperationResponseDto> RebalanceStockAsync([Body] RebalanceStockDto request, CancellationToken cancellationToken = default);
    [Post("/api/Inventory/item")]
    Task<StockOperationResponseDto> CreateItemAsync([Body] CreateInventoryItemDto request, CancellationToken cancellationToken = default);
    [Delete("/api/Inventory/item/{warehouseCode}/{sku}")]
    Task<StockOperationResponseDto> DeleteItemAsync(string warehouseCode, string sku, CancellationToken cancellationToken = default);
    [Get("/api/Inventory/forecast/{sku}")]
    Task<ForecastResponseDto> GetForecastAsync(string sku, [Query] int days = 7, CancellationToken cancellationToken = default);
    [Post("/api/Inventory/snapshot")]
    Task<StockOperationResponseDto> CreateSnapshotAsync([Body] CreateSnapshotDto request, CancellationToken cancellationToken = default);
    [Post("/api/Inventory/snapshot/{snapshotId}/restore")]
    Task<StockOperationResponseDto> RestoreSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default);


    //analitik
    [Get("/api/Order/analytics")]
    Task<OrderAnalyticsResponseDto> GetAnalyticsAsync([Query] DateTime? from, [Query] DateTime? to, CancellationToken cancellationToken = default);



}
