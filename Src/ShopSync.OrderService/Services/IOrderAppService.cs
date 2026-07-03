using ShopSync.OrderService.Dtos;
using ShopSync.OrderService.Repositories;

namespace ShopSync.OrderService.Services;

public interface IOrderAppService
{
    
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderResponse> GetOrderAsync(string orderId, CancellationToken ct = default);
    Task<PagedResponse<OrderResponse>> ListOrdersAsync(OrderFilter filter, CancellationToken ct = default);
    Task<OrderResponse> CancelOrderAsync(string orderId, CancelOrderRequest? request, CancellationToken ct = default);
}
