using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    // ReserveBatch metodu, birden fazla ürünün stoklarını rezerve etmek için kullanılır.
    public override async Task<ReserveBatchResponse> ReserveBatch(ReserveBatchRequest request, ServerCallContext context)
    {

        // Log isteğin alındığını ve kaç ürün olduğunu
        _logger.LogInformation(
          "ReserveBatch isteği alındı. OrderId: {OrderId}, Ürün sayısı: {Count}",
          request.OrderId, request.Items.Count);

        // 1. İstekteki SKU'ları normalize et ve aynı SKU'ları birleştir 
        var consolidatedItems = request.Items
        .GroupBy(i => i.Sku.Trim().ToUpperInvariant())
        .Select(g => new
        {
            NormalizedSku = g.Key, //"abc-123" ve "ABC-123" aynı SKU olarak kabul edilir
            OriginalSku = g.First().Sku, // İlk SKU'yu orijinal SKU olarak al
            TotalQuantity = g.Sum(i => i.Quantity) // Aynı SKU için toplam miktarı hesapla
        })
        .ToList();


        // İstekteki SKU kodlarını çıkar
        var skus = consolidatedItems.Select(i => i.NormalizedSku).ToList();
        try
        {
            // SKU'ları alfabetik sırayla kilitle
            await using var lockHandle = await _lockService.AcquireLocksAsync(skus, cancellationToken: context.CancellationToken);

            // MongoDB'den stokları getir
            var inventoryItems = await _repository.GetBySkusAsync(skus, context.CancellationToken);

            // Hızlı erişim için Dictionary'ye çevir (SKU → InventoryItem)
            var inventoryMap = inventoryItems.ToDictionary(i => i.Sku.Trim().ToUpperInvariant());

            // Önce eksik SKU'ları kontrol et
            var missingSkus = skus.Where(s => !inventoryMap.ContainsKey(s)).ToList();

            if (missingSkus.Count > 0)
            {
                _logger.LogWarning("ReserveBatch başarısız. Bulunamayan SKU'lar: {Skus}", string.Join(", ", missingSkus));
                return new ReserveBatchResponse
                {
                    Success = false,
                    Message = $"Şu SKU'lar bulunamadı: {string.Join(", ", missingSkus)}"
                };
            }
            // Stok yeterliliği kontrolü 
            var failedItems = consolidatedItems
                .Where(item => !inventoryMap[item.NormalizedSku].CanReserve(item.TotalQuantity))
                .Select(item => new FailedItem
                {
                    Sku = item.OriginalSku,
                    AvailableQuantity = inventoryMap[item.NormalizedSku].QuantityAvailable,
                    RequestedQuantity = item.TotalQuantity,
                    Reason = "Yetersiz stok"
                })
                .ToList();

            if (failedItems.Count > 0)
            {
                _logger.LogWarning("ReserveBatch başarısız. Yetersiz stok: {Count} ürün", failedItems.Count);
                return new ReserveBatchResponse
                {
                    Success = false,
                    Message = "Bazı ürünler rezerve edilemedi. Hiçbir ürün rezerve edilmedi.",
                    FailedItems = { failedItems }
                };
            }


            using var session = await _dbContext.Client.StartSessionAsync(
            cancellationToken: context.CancellationToken);
            session.StartTransaction();
          

            try
            {
                foreach (var requestedItem in consolidatedItems)
                {

                    var stock = inventoryMap[requestedItem.NormalizedSku];


                    // İşlem öncesi değerleri kaydet
                    var prevAvailable = stock.QuantityAvailable;
                    var prevReserved = stock.QuantityReserved;

                    // Stok güncelle DDD ile
                    stock.Reserve(requestedItem.TotalQuantity);


                    // MongoDB'ye yaz (transaction içinde)
                    await _repository.UpdateAsync(stock, session, context.CancellationToken);

                    var log = new InventoryTransactionLog(
                        sku: requestedItem.OriginalSku,
                        transactionType: InventoryTransactionType.Reserve,
                        quantity: requestedItem.TotalQuantity,
                        previousAvailable: prevAvailable,
                        newAvailable: stock.QuantityAvailable,
                        previousReserved: prevReserved,
                        newReserved: stock.QuantityReserved,
                        orderId: request.OrderId,
                        reason: "ReserveBatch");

                    await _repository.AddTransactionLogAsync(
                        log,
                        session,
                        context.CancellationToken);

                }

                // Tüm güncellemeler başarılı → Transaction'ı onayla (commit)
                await session.CommitTransactionAsync(context.CancellationToken);
                _logger.LogInformation(
                    "ReserveBatch başarılı. OrderId: {OrderId}", request.OrderId);
                return new ReserveBatchResponse
                {
                    Success = true,
                    Message = "Tüm ürünler başarıyla rezerve edildi."
                };
            }
            catch (Exception ex)
            {

                // Herhangi bir hata olursa transaction'ı geri al (rollback)
                await session.AbortTransactionAsync(context.CancellationToken);
                _logger.LogError(ex,
                    "ReserveBatch transaction hatası. OrderId: {OrderId}", request.OrderId);
                throw;
            }



        }
        catch (TimeoutException ex)
        {
            // Distributed Lock alınamadı (tüm denemeler tükendi)
            _logger.LogError(ex,
                "ReserveBatch lock zaman aşımı. OrderId: {OrderId}", request.OrderId);
            return new ReserveBatchResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            // Beklenmeyen hata
            _logger.LogError(ex,
                "ReserveBatch beklenmeyen hata. OrderId: {OrderId}", request.OrderId);
            throw new RpcException(
                new Status(StatusCode.Internal, $"Sunucu hatası: {ex.Message}"));
        }
    }
}
