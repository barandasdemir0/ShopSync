using Mapster;
using ShopSync.InventoryService.Protos;
using ShopSync.OrderService.Dtos;
using ShopSync.OrderService.Exceptions;
using ShopSync.OrderService.Infrastructure.GrpcClients;
using ShopSync.OrderService.Infrastructure.Idempotency;
using ShopSync.OrderService.Models;
using ShopSync.OrderService.Repositories;

namespace ShopSync.OrderService.Services;

public sealed class OrderAppService : IOrderAppService
{


    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryGrpcClient _inventoryClient;
    private readonly IIdempotencyService _idempotencyService;

    public OrderAppService(IOrderRepository orderRepository, IInventoryGrpcClient inventoryClient, IIdempotencyService idempotencyService)
    {
        _orderRepository = orderRepository;
        _inventoryClient = inventoryClient;
        _idempotencyService = idempotencyService;
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
        // Redis için expire süresi
        var idempotencyExpiry = TimeSpan.FromHours(24); // İdempotency anahtarının geçerlilik süresi
        bool idempotencyLockAcquired = false; // İdempotency kilidinin alınıp alınmadığını takip etmek için bir bayrak

        // İdempotency kontrolü
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            // İdempotency anahtarını Redis'te kontrol et
            var existingOrderId = await _idempotencyService.GetOrderIdAsync(request.IdempotencyKey, ct);
            // Eğer daha önce aynı idempotency key ile bir sipariş oluşturulmuşsa, o siparişi döndür
            if (existingOrderId is not null && existingOrderId != "PROCESSING")
            {
                // Varsa direkt dön
                var existingOrder = await _orderRepository.GetByOrderIdAsync(existingOrderId, ct);
                if (existingOrder is not null)
                {
                    return existingOrder.Adapt<OrderResponse>();
                }
            }

            // B. İlk defa geliyorsa kilidi al 
            var acquired = await _idempotencyService.TryAcquireAsync(request.IdempotencyKey, idempotencyExpiry, ct);
            if (!acquired)
            {
                throw new DomainException("Bu istek şu an işleniyor. Lütfen biraz bekleyin.", "IDEMPOTENCY_IN_PROGRESS");
            }
            idempotencyLockAcquired = true;


        }

        // Domain nesnesi yarat
        var orderId = Guid.NewGuid().ToString("N");

        // Siparişin line itemlarını oluştur
        var lineItems = request.Items.Select(i => new OrderLineItem(i.Sku, i.Quantity)).ToList();

        // Sipariş nesnesini yarat
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

        // İdempotency anahtarını Redis'e kaydet
        if (idempotencyLockAcquired)
        {
            // İdempotency anahtarını Redis'e kaydet ve sipariş ID'sini ilişkilendir
            await _idempotencyService.SetOrderIdAsync(request.IdempotencyKey!, orderId, idempotencyExpiry, ct);
        }
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
