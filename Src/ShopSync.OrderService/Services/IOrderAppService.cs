using ShopSync.OrderService.Dtos;
using ShopSync.OrderService.Repositories;

namespace ShopSync.OrderService.Services;

public interface IOrderAppService
{
    
    Task<OrderResponseDto> CreateOrderAsync(CreateOrderRequestDto request, CancellationToken ct = default);
    Task<OrderResponseDto> GetOrderAsync(string orderId, CancellationToken ct = default);
    Task<PagedResponseDto<OrderResponseDto>> ListOrdersAsync(OrderFilter filter, CancellationToken ct = default);
    Task<OrderResponseDto> CancelOrderAsync(string orderId, CancelOrderRequestDto? request, CancellationToken ct = default);

    // siparişleri onaylamak içi metod 
    Task<OrderResponseDto> ConfirmOrderAsync(string orderId, CancellationToken ct = default);

    // siparişleri toplu olarak iptal etmek için metod
    Task<BatchCancelResponseDto> BatchCancelAsync(BatchCancelRequestDto request, CancellationToken ct = default);

    // siparişleri admin override ile iptal etmek için metod
    Task<OrderResponseDto> AdminOverrideCancelAsync(string orderId, string reason, CancellationToken ct = default);


}
