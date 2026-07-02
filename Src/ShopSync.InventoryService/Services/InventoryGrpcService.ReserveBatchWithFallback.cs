using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    public override async Task<ReserveBatchResponse> ReserveBatchWithFallback(ReserveBatchWithFallbackRequest request, ServerCallContext context)
    {
        _logger.LogInformation(
         "ReserveBatchWithFallback isteği. OrderId: {OrderId}, " +
         "Primary: {Primary}, Fallbacks: [{Fallbacks}], Ürün sayısı: {Count}",
         request.OrderId, request.PrimaryWarehouse,
         string.Join(", ", request.FallbackWarehouses),
         request.Items.Count);


        var warehouseOrder = new List<string>
        {
            request.PrimaryWarehouse.Trim().ToUpperInvariant()
        };

        warehouseOrder.AddRange(request.FallbackWarehouses.Select(x => x.Trim().ToUpperInvariant()));


        var consolidatedItems = request.Items
            .GroupBy(x => x.Sku.Trim().ToUpperInvariant())
            .Select(g => new
            {
                NormalizedSku = g.Key,
                OriginalSku = g.First().Sku,
                TotalQuantity = g.Sum(i => i.Quantity)
            })
            .ToList();


        var reservationPlan = new Dictionary<string, (InventoryItem Stock, int Quantity, string OriginalSku)>();

        //skuların tüm depolarda olup olmadığını kontrol et
        var allSkus = consolidatedItems.Select(i => i.NormalizedSku).ToList();

        try
        {
            // Tüm SKU'lar için tüm depolardaki lock key'leri oluştur
            var lockKeys = allSkus
                .SelectMany(sku => warehouseOrder.Select(wh => $"{sku}:{wh}"))
                .Distinct()
                .ToList();


            await using var lockHandle = await _lockService.AcquireLocksAsync(
            lockKeys, cancellationToken: context.CancellationToken);
            // Her SKU için en uygun depoyu bul
            var failedItems = new List<FailedItem>();


            foreach (var item in consolidatedItems)
            {
                var allWarehouses = await _repository.GetBySkuAllWarehousesAsync(
                  item.NormalizedSku, context.CancellationToken);


                // Depo öncelik sırasına göre yeterli stoğu olan ilk depoyu bul
                InventoryItem? selectedStock = null;

                foreach (var warehouse in warehouseOrder)
                {
                    var candidate = allWarehouses
                        .FirstOrDefault(s => s.WarehouseCode == warehouse);

                    if (candidate is not null && candidate.CanReserve(item.TotalQuantity))
                    {
                        selectedStock = candidate;
                        break; // Bu depoda yeterli stok var, aramayı durdur
                    }

                   
                }
                if (selectedStock is null)
                {
                    // Hiçbir depoda yeterli stok yok
                    failedItems.Add(new FailedItem
                    {
                        Sku = item.OriginalSku,
                        RequestedQuantity = item.TotalQuantity,
                        AvailableQuantity = allWarehouses.Sum(s => s.QuantityAvailable),
                        Reason = "Hiçbir depoda yeterli stok bulunamadı"
                    });
                }
                // Eğer bir depo bulunduysa, rezervasyon planına ekle
                else
                {
                    reservationPlan[item.NormalizedSku] = (selectedStock, item.TotalQuantity, item.OriginalSku);
                }


            }

            // All-or-nothing: Herhangi bir SKU başarısızsa HİÇBİRİNİ rezerve etme
            if (failedItems.Count > 0)
            {
                _logger.LogWarning(
                    "ReserveBatchWithFallback başarısız. Yetersiz stok: {Count} ürün",
                    failedItems.Count);
                return new ReserveBatchResponse
                {
                    Success = false,
                    Message = "Bazı ürünler hiçbir depoda rezerve edilemedi. All-or-nothing: Hiçbir ürün rezerve edilmedi.",
                        FailedItems =
                        {

                            failedItems
                        }
                };

               
            }

            // Tüm SKU'lar için uygun depo bulundu → Transaction başlat
            using var session = await _dbContext.Client.StartSessionAsync(
                cancellationToken: context.CancellationToken);
            session.StartTransaction();

            try
            {
                foreach (var kvp in reservationPlan)
                {
                    var (stock, quantity, originalSku) = kvp.Value;

                    var prevAvailable = stock.QuantityAvailable;
                    var prevReserved = stock.QuantityReserved;

                    // DDD metodu ile rezervasyon yap
                    stock.Reserve(quantity);

                    await _repository.UpdateAsync(stock, session, context.CancellationToken);

                    var log = new InventoryTransactionLog(
                        sku: originalSku,
                        transactionType: InventoryTransactionType.Reserve,
                        quantity: quantity,
                        previousAvailable: prevAvailable,
                        newAvailable: stock.QuantityAvailable,
                        previousReserved: prevReserved,
                        newReserved: stock.QuantityReserved,
                        orderId: request.OrderId,
                        reason: $"ReserveBatchWithFallback (Depo: {stock.WarehouseCode})");
                    await _repository.AddTransactionLogAsync(log, session, context.CancellationToken);
                }

                await session.CommitTransactionAsync(context.CancellationToken);
                _logger.LogInformation(
                    "ReserveBatchWithFallback başarılı. OrderId: {OrderId}",
                    request.OrderId);
                return new ReserveBatchResponse
                {
                    Success = true,
                    Message = "Tüm ürünler başarıyla rezerve edildi (fallback desteğiyle)."
                };
            }
            catch (Exception ex)
            {

                await session.AbortTransactionAsync(context.CancellationToken);
                _logger.LogError(ex,
                    "ReserveBatchWithFallback transaction hatası. OrderId: {OrderId}",
                    request.OrderId);
                throw;
            }



        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex,
                "ReserveBatchWithFallback lock zaman aşımı. OrderId: {OrderId}",
                request.OrderId);
            return new ReserveBatchResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }










    }
}
