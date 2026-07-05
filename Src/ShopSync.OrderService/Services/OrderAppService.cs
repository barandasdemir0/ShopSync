using Mapster;
using ShopSync.InventoryService.Protos;
using ShopSync.OrderService.Dtos;
using ShopSync.OrderService.Exceptions;
using ShopSync.OrderService.Infrastructure.GrpcClients;
using ShopSync.OrderService.Infrastructure.Idempotency;
using ShopSync.OrderService.Models;
using ShopSync.OrderService.Repositories;
using StackExchange.Redis;

namespace ShopSync.OrderService.Services;

public sealed class OrderAppService : IOrderAppService
{


    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryGrpcClient _inventoryClient;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<OrderAppService> _logger;

    public OrderAppService(IOrderRepository orderRepository, IInventoryGrpcClient inventoryClient, IIdempotencyService idempotencyService, ILogger<OrderAppService> logger)
    {
        _orderRepository = orderRepository;
        _inventoryClient = inventoryClient;
        _idempotencyService = idempotencyService;
        _logger = logger;
    }

    public async Task<OrderResponseDto> AdminOverrideCancelAsync(string orderId, string reason, CancellationToken ct = default)
    {
        _logger.LogWarning("[ADMIN OVERRIDE] Sipariş iptal isteği. OrderId: {OrderId}, Sebep: {Reason}",
       orderId, reason);

        var order = await _orderRepository.GetByOrderIdAsync(orderId, ct)
       ?? throw new DomainException($"Sipariş bulunamadı: {orderId}", "ORDER_NOT_FOUND");


        // Domain metodu 
        order.AdminOverrideCancel(reason);

        // Eğer hâlâ InventoryService'te kalmış stok varsa serbest bırak
        try
        {
            var releaseItems = order.LineItems.Select(li =>
                new ReservationItem
                {
                    Sku = li.Sku,
                    Quantity = li.RequestedQuantity
                });
            await _inventoryClient.ReleaseBatchAsync(orderId, releaseItems, ct);
        }

        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[ADMIN OVERRIDE] Stok serbest bırakma başarısız. " +
                "Stoklar zaten expiration job tarafından bırakılmış olabilir. OrderId: {OrderId}",
                orderId);
        }
        await _orderRepository.UpdateAsync(order, ct);
        _logger.LogWarning("[ADMIN OVERRIDE] Sipariş başarıyla iptal edildi. OrderId: {OrderId}", orderId);
        var response = order.Adapt<OrderResponseDto>();
        return response;

    }

    public async Task<BatchCancelResponseDto> BatchCancelAsync(BatchCancelRequestDto request, CancellationToken ct = default)
    {
        _logger.LogInformation("Toplu iptal isteği alındı. Sipariş sayısı: {Count}", request.OrderIds.Count);


        var orders = await _orderRepository.GetByOrderIdsAsync(request.OrderIds, ct);
        var response = new BatchCancelResponseDto
        {
            TotalRequested = request.OrderIds.Count
        };


        foreach (var orderId in request.OrderIds)
        {
            var order = orders.FirstOrDefault(o => o.OrderId == orderId);

            if (order is null)
            {
                response.Results.Add(new BatchCancelItemResultDto
                {
                    OrderId = orderId,
                    Success = false,
                    Message = $"Sipariş bulunamadı: {orderId}"
                });
                response.FailedCount++;
                continue;
            }

            try
            {
                // Domain metodu ile iptal et
                order.Cancel(request.Reason);
                // InventoryService'e stokları serbest bırak
                var releaseItems = order.LineItems.Select(li =>
               new ReservationItem 
               {
                   Sku = li.Sku,
                   Quantity = li.RequestedQuantity
               });
                await _inventoryClient.ReleaseBatchAsync(orderId, releaseItems, ct);
                await _orderRepository.UpdateAsync(order, ct);
                response.Results.Add(new BatchCancelItemResultDto
                {
                    OrderId = orderId,
                    Success = true,
                    Message = "Sipariş başarıyla iptal edildi."
                });
                response.SuccessCount++;
            }
            catch (DomainException ex)
            {

                response.Results.Add(new BatchCancelItemResultDto
                {
                    OrderId = orderId,
                    Success = false,
                    Message = ex.Message
                });
                response.FailedCount++;
            }
        }

        _logger.LogInformation(
      "Toplu iptal tamamlandı. Başarılı: {Success}, Başarısız: {Failed}",
      response.SuccessCount, response.FailedCount);
        return response;
    }

    public async Task<OrderResponseDto> CancelOrderAsync(string orderId, CancelOrderRequestDto? request, CancellationToken ct = default)
    {
        _logger.LogInformation("Sipariş iptal  isteği alındı. OrderId: {OrderId}", orderId);


        // Siparişi veritabanından getir
        var order = await _orderRepository.GetByOrderIdAsync(orderId, ct);

        //Siparişin bulunup bulunmadığını kontrol et 
        if (order == null)
        {
            _logger.LogWarning(
            "İptal edilmek istenen sipariş bulunamadı. OrderId: {OrderId}",
            orderId);
            throw new ArgumentException($"Sipariş bulunamadı: {orderId}");
        }

        _logger.LogInformation(
       "İptal edilecek sipariş bulundu. OrderId: {OrderId}, CurrentStatus: {Status}, ItemCount: {ItemCount}",
       orderId,
       order.Status,
       order.LineItems.Count);

        //  İptal sebebini güvenli bir şekilde al 
        string? cancelReason = null;
        if (request != null)
        {
            cancelReason = request.Reason;
        }

        //İptal işlemini gerçekleştir
        order.Cancel(cancelReason);

        _logger.LogInformation("Sipariş durumu CANCELLED olarak güncellendi. Sebep: {Reason}", cancelReason ?? "Belirtilmedi");


        // Stoktan düşülecek ürünleri hazırla
        var releaseItems = new List<ReservationItem>();

        // Siparişin line itemlarını dolaş ve her birini releaseItems listesine ekle
        foreach (var li in order.LineItems)
        {
            var reservationItem = new ReservationItem
            {
                Sku = li.Sku,
                Quantity = li.RequestedQuantity
            };

            releaseItems.Add(reservationItem);
        }

        _logger.LogInformation(
    "InventoryService'e stokları serbest bırakma isteği gönderiliyor. OrderId: {OrderId}, ItemCount: {ItemCount}",
    orderId,
    releaseItems.Count);
        var releaseResponse = await _inventoryClient.ReleaseBatchAsync(orderId, releaseItems, ct);

        if (releaseResponse is not null && !releaseResponse.Success)
        {
            _logger.LogWarning(
                "InventoryService stok serbest bırakma işleminde başarısız cevap döndü. OrderId: {OrderId}, Message: {Message}",
                orderId,
                releaseResponse.Message);
        }
        else
        {
            _logger.LogInformation(
                "InventoryService stok serbest bırakma işlemi tamamlandı. OrderId: {OrderId}",
                orderId);
        }


        //Siparişin yeni durumunu veritabanına kaydet
        await _orderRepository.UpdateAsync(order, ct);


        _logger.LogInformation(
            "Sipariş iptali veritabanına kaydedildi. OrderId: {OrderId}, Status: {Status}",
            orderId,
            order.Status);

        // Sipariş nesnesini, dışarıya döneceğimiz cevap (Response) modeline dönüştür
        var response = order.Adapt<OrderResponseDto>();

        _logger.LogInformation(
       "Sipariş iptal işlemi başarıyla tamamlandı. OrderId: {OrderId}",
       orderId);


        // Sonucu döndür
        return response;
    }

    public async Task<OrderResponseDto> ConfirmOrderAsync(string orderId, CancellationToken ct = default)
    {
        _logger.LogInformation("Sipariş onaylama (Confirm) isteği alındı. OrderId: {OrderId}", orderId);

        var order = await _orderRepository.GetByOrderIdAsync(orderId, ct)
            ?? throw new DomainException($"Sipariş bulunamadı: {orderId}", "ORDER_NOT_FOUND");

        order.Confirm();
        _logger.LogInformation("Sipariş durumu CONFIRMED olarak güncellendi. OrderId: {OrderId}", orderId);

        // InventoryService'e onay gönder → Stok kalıcı olarak düşsün
        _logger.LogInformation("InventoryService'e ConfirmReservation gRPC isteği atılıyor. OrderId: {OrderId}", orderId);
        var confirmResponse = await _inventoryClient.ConfirmReservationAsync(orderId, ct);
        if (!confirmResponse.Success)
        {

            _logger.LogError(
                "InventoryService stok onaylama başarısız oldu! OrderId: {OrderId}, Mesaj: {Message}. " +
                "Not: Sipariş veritabanında onaylandı, reconciliation işlemi tutarsızlığı giderecek.",
                orderId, confirmResponse.Message);
        }
        else
        {
            _logger.LogInformation("InventoryService rezervasyonu kalıcılaştırdı. OrderId: {OrderId}", orderId);
        }
        await _orderRepository.UpdateAsync(order, ct);
        _logger.LogInformation("Sipariş onayı MongoDB'ye kaydedildi. OrderId: {OrderId}", orderId);
        return order.Adapt<OrderResponseDto>();

    }

    public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderRequestDto request, CancellationToken ct = default)
    {
        _logger.LogInformation("Sipariş oluşturma işlemi başladı. IdempotencyKey: {IdempotencyKey}, Ürün Sayısı: {ItemCount}",
             request.IdempotencyKey, request.Items.Count);

        // Redis için expire süresi
        var idempotencyExpiry = TimeSpan.FromHours(24); // İdempotency anahtarının geçerlilik süresi
        bool idempotencyLockAcquired = false; // İdempotency kilidinin alınıp alınmadığını takip etmek için bir bayrak

        // İdempotency kontrolü
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            _logger.LogDebug("Redis üzerinden idempotency kontrolü yapılıyor. Key: {Key}", request.IdempotencyKey);
            // İdempotency anahtarını Redis'te kontrol et
            var existingOrderId = await _idempotencyService.GetOrderIdAsync(request.IdempotencyKey, ct);
            // Eğer daha önce aynı idempotency key ile bir sipariş oluşturulmuşsa, o siparişi döndür
            if (existingOrderId is not null && existingOrderId != "PROCESSING")
            {
                _logger.LogInformation("Bu IdempotencyKey ({Key}) ile daha önce {OrderId} siparişi oluşturulmuş. Mevcut sipariş dönülüyor.", request.IdempotencyKey, existingOrderId);
                // Varsa direkt dön
                var existingOrder = await _orderRepository.GetByOrderIdAsync(existingOrderId, ct);
                if (existingOrder is not null)
                {
                    return existingOrder.Adapt<OrderResponseDto>();
                }
            }

            // B. İlk defa geliyorsa kilidi al 
            var acquired = await _idempotencyService.TryAcquireAsync(request.IdempotencyKey, idempotencyExpiry, ct);
            if (!acquired)
            {
                _logger.LogWarning("Idempotency key şu an başka bir thread tarafından kilitlenmiş. Key: {Key}", request.IdempotencyKey);
                throw new DomainException("Bu istek şu an işleniyor. Lütfen biraz bekleyin.", "IDEMPOTENCY_IN_PROGRESS");
            }
            idempotencyLockAcquired = true;


        }

        // Domain nesnesi yarat
        var orderId = Guid.NewGuid().ToString("N");

        // Siparişin line itemlarını oluştur
        var lineItems = request.Items.Select(i => new OrderLineItem(i.Sku, i.Quantity)).ToList();

        // Sipariş nesnesini yarat
        var order = new Models.Order(orderId, lineItems, request.IdempotencyKey);

        _logger.LogInformation("Yeni sipariş oluşturuldu (Domain). OrderId: {OrderId}", orderId);


        // gRPC ile Envantere Git
        var reservationItems = request.Items.Select(i => new ReservationItem
        {
            Sku = i.Sku,
            Quantity = i.Quantity
        });


        _logger.LogInformation("InventoryService'e stok rezervasyonu isteği gönderiliyor. OrderId: {OrderId}", orderId);
        // Envanter servisine rezervasyon isteği gönderiyoruz. Eğer rezervasyon başarısız olursa, DomainException fırlatıyoruz.
        var reserveResponse = await _inventoryClient.ReserveBatchAsync(orderId, reservationItems, ct);
        if (!reserveResponse.Success)
        {
            _logger.LogError("InventoryService rezervasyon işlemi BAŞARISIZ! OrderId: {OrderId}, Hata: {Message}", orderId, reserveResponse.Message);

            throw new DomainException(reserveResponse.Message, "RESERVATION_FAILED");
        }

        _logger.LogInformation("InventoryService rezervasyon işlemi BAŞARILI. OrderId: {OrderId}", orderId);

        // Eğer rezervasyon başarılı ise, siparişi veritabanına kaydediyoruz.
        order.MarkItemsAsReserved();
        await _orderRepository.InsertAsync(order, ct);

        // İdempotency anahtarını Redis'e kaydet
        if (idempotencyLockAcquired)
        {
            // İdempotency anahtarını Redis'e kaydet ve sipariş ID'sini ilişkilendir
            await _idempotencyService.SetOrderIdAsync(request.IdempotencyKey!, orderId, idempotencyExpiry, ct);

            _logger.LogDebug("Sipariş ID'si Redis Idempotency cache'ine kaydedildi. OrderId: {OrderId}", orderId);
        }
        return order.Adapt<OrderResponseDto>();
    }

    // Sipariş detayını getir
    public async Task<OrderResponseDto> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByOrderIdAsync(orderId, ct);

        if (order is null)
        {
            throw new ArgumentException(
                $"Sipariş bulunamadı: {orderId}",
                nameof(orderId));
        }

        var response = order.Adapt<OrderResponseDto>();

        return response;
    }

    // Siparişleri filtreleyerek listele
    public async Task<PagedResponseDto<OrderResponseDto>> ListOrdersAsync(OrderFilter filter, CancellationToken ct = default)
    {
        var orders = await _orderRepository.ListOrdersAsync(filter, ct);
        var responseList = orders.Adapt<List<OrderResponseDto>>();
        return new PagedResponseDto<OrderResponseDto>
        {
            Page = filter.Page,
            PageSize = filter.PageSize,
            Count = responseList.Count,
            Data = responseList
        };
    }
}
