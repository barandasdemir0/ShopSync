using Mapster;
using ShopSync.InventoryService.Protos;
using ShopSync.OrderService.Dtos;
using ShopSync.OrderService.Exceptions;
using ShopSync.OrderService.Infrastructure.GrpcClients;
using ShopSync.OrderService.Models;
using ShopSync.OrderService.Repositories;

namespace ShopSync.OrderService.Services;

public sealed class OrderAppService : IOrderAppService
{


    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryGrpcClient _inventoryClient;
    public OrderAppService(IOrderRepository orderRepository, IInventoryGrpcClient inventoryClient)
    {
        _orderRepository = orderRepository;
        _inventoryClient = inventoryClient;
    }


    public async Task<OrderResponse> CancelOrderAsync(string orderId, CancelOrderRequest? request, CancellationToken ct = default)
    {
        // Siparişi veritabanından getir
        var order = await _orderRepository.GetByOrderIdAsync(orderId, ct);

        //Siparişin bulunup bulunmadığını kontrol et 
        if (order == null)
        {
            throw new ArgumentException($"Sipariş bulunamadı: {orderId}");
        }

        //  İptal sebebini güvenli bir şekilde al 
        string? cancelReason = null;
        if (request != null)
        {
            cancelReason = request.Reason;
        }

        //İptal işlemini gerçekleştir
        order.Cancel(cancelReason);

        // Stoktan düşülecek ürünleri hazırla
        var releaseItems = new List<ReservationItem>();

        foreach (var li in order.LineItems)
        {
            var reservationItem = new ReservationItem
            {
                Sku = li.Sku,
                Quantity = li.RequestedQuantity
            };

            releaseItems.Add(reservationItem);
        }

        // Envanter servisine haber ver ve rezerve edilen ürünleri serbest bırak
        await _inventoryClient.ReleaseBatchAsync(orderId, releaseItems, ct);

        //Siparişin yeni durumunu veritabanına kaydet
        await _orderRepository.UpdateAsync(order, ct);

        // Sipariş nesnesini, dışarıya döneceğimiz cevap (Response) modeline dönüştür
        var response = order.Adapt<OrderResponse>();

        // Sonucu döndür
        return response;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        // İdempotency kontrolü
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingOrder = await _orderRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
            if (existingOrder is not null)
            {
                return existingOrder.Adapt<OrderResponse>();
            }
        }

        // Domain nesnesi yarat
        var orderId = Guid.NewGuid().ToString("N");

        var lineItems = request.Items.Select(i => new OrderLineItem(i.Sku, i.Quantity)).ToList();

        var order = new Order(orderId, lineItems, request.IdempotencyKey);

        // gRPC ile Envantere Git
        var reservationItems = request.Items.Select(i => new ReservationItem 
        { 
            Sku = i.Sku, 
            Quantity = i.Quantity 
        });

        // Envanter servisine rezervasyon isteği gönderiyoruz. Eğer rezervasyon başarısız olursa, DomainException fırlatıyoruz.
        var reserveResponse = await _inventoryClient.ReserveBatchAsync(orderId, reservationItems, ct);
        if (!reserveResponse.Success)
        {
            
            throw new DomainException(reserveResponse.Message, "RESERVATION_FAILED");
        }

        // Eğer rezervasyon başarılı ise, siparişi veritabanına kaydediyoruz.
        order.MarkItemsAsReserved();
        await _orderRepository.InsertAsync(order, ct);
        return order.Adapt<OrderResponse>();
    }

    // Sipariş detayını getir
    public async Task<OrderResponse> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByOrderIdAsync(orderId, ct);

        if (order is null)
        {
            throw new ArgumentException(
                $"Sipariş bulunamadı: {orderId}",
                nameof(orderId));
        }

        var response = order.Adapt<OrderResponse>();

        return response;
    }

    // Siparişleri filtreleyerek listele
    public async Task<PagedResponse<OrderResponse>> ListOrdersAsync(OrderFilter filter, CancellationToken ct = default)
    {
        var orders = await _orderRepository.ListOrdersAsync(filter, ct);
        var responseList = orders.Adapt<List<OrderResponse>>();
        return new PagedResponse<OrderResponse>
        {
            Page = filter.Page,
            PageSize = filter.PageSize,
            Count = responseList.Count,
            Data = responseList
        };
    }
}
