using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{

    public override async Task<StockOperationResponse> ConfirmReservation(ConfirmReservationRequest request, ServerCallContext context)
    {
        _logger.LogInformation(
          "ConfirmReservation isteği alındı. OrderId: {OrderId}, Ürün sayısı: {Count}",
          request.OrderId, request.Items.Count);

        //istekleri SKU bazında birleştir 
        var consolidatedItems = request.Items
          .GroupBy(i => i.Sku.Trim().ToUpperInvariant())
          .Select(g => new
          {
              NormalizedSku = g.Key,
              OriginalSku = g.First().Sku,
              TotalQuantity = g.Sum(i => i.Quantity)
          })
          .ToList();

        var skus = consolidatedItems.Select(i => i.NormalizedSku).ToList();

        try
        {
            // SKU'ları alfabetik sırayla kilitle 
            await using var lockHandle = await _lockService.AcquireLocksAsync(
                skus, cancellationToken: context.CancellationToken);

            // MongoDB'den stokları getir
            var allInventoryItems = await _repository.GetBySkusAsync(skus, context.CancellationToken);
            var confirmPlan = new Dictionary<string, InventoryItem>();
            var missingSkus = new List<string>();
            var insufficientSkus = new List<string>();
            foreach (var item in consolidatedItems)
            {
                var stocksForSku = allInventoryItems
                    .Where(i => i.Sku.Trim().ToUpperInvariant() == item.NormalizedSku)
                    .ToList();
                if (stocksForSku.Count == 0)
                {
                    missingSkus.Add(item.OriginalSku);
                    continue;
                }
                // Rezerve edilmiş miktarı karşılayan (siparişin beklediği depoyu) bul
                var suitableStock = stocksForSku.FirstOrDefault(s => s.QuantityReserved >= item.TotalQuantity);
                if (suitableStock != null)
                {
                    confirmPlan[item.NormalizedSku] = suitableStock;
                }
                else
                {
                    insufficientSkus.Add(item.OriginalSku);
                }
            }
            // Eksik SKU kontrolü
            if (missingSkus.Count > 0)
            {
                _logger.LogWarning("ConfirmReservation: SKU'lar bulunamadı: {Skus}", string.Join(", ", missingSkus));
                return new StockOperationResponse
                {
                    Success = false,
                    Message = $"Şu SKU'lar bulunamadı: {string.Join(", ", missingSkus)}"
                };
            }
            // Yetersiz rezervasyon varsa hata döndür
            if (insufficientSkus.Count > 0)
            {
                _logger.LogWarning("ConfirmReservation başarısız. Yetersiz rezervasyon: {Skus}", string.Join(", ", insufficientSkus));
                return new StockOperationResponse
                {
                    Success = false,
                    Message = $"Şu SKU'lar için yeterli rezervasyon yok: {string.Join(", ", insufficientSkus)}"
                };
            }

            // MongoDB Transaction başlat
            using var session = await _dbContext.Client.StartSessionAsync(
                cancellationToken: context.CancellationToken);
            session.StartTransaction();

            try
            {
                // Rezervasyonları onayla ve stokları güncelle
                foreach (var requestedItem in consolidatedItems)
                {

                    var stock = confirmPlan[requestedItem.NormalizedSku];
                    var prevAvailable = stock.QuantityAvailable;
                    var prevReserved = stock.QuantityReserved;


                    // DDD metodu: Reserved azalt, Available'a geri ekleme 
                    stock.ConfirmReservation(requestedItem.TotalQuantity);

                    await _repository.UpdateAsync(
                      stock, session, context.CancellationToken);


                    // InventoryTransactionType'a "Confirm" eklenmeli (Adım 15.4'te)
                    var log = new InventoryTransactionLog(
                        sku: requestedItem.OriginalSku,
                        transactionType: InventoryTransactionType.Confirm,
                        quantity: requestedItem.TotalQuantity,
                        previousAvailable: prevAvailable,
                        newAvailable: stock.QuantityAvailable,
                        previousReserved: prevReserved,
                        newReserved: stock.QuantityReserved,
                        orderId: request.OrderId,
                        reason: "ConfirmReservation");

                    await _repository.AddTransactionLogAsync(
                      log, session, context.CancellationToken);
                }

                await session.CommitTransactionAsync(context.CancellationToken);
                _metrics.ReservationsConfirmed.Add(1);

                _logger.LogInformation(
                    "ConfirmReservation başarılı. OrderId: {OrderId}", request.OrderId);

                return new StockOperationResponse
                {
                    Success = true,
                    Message = "Tüm rezervasyonlar başarıyla onaylandı."
                };
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync(context.CancellationToken);
                _logger.LogError(ex,
                    "ConfirmReservation transaction hatası. OrderId: {OrderId}",
                    request.OrderId);
                throw;
            }

        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex,
                "ConfirmReservation lock zaman aşımı. OrderId: {OrderId}",
                request.OrderId);
            return new StockOperationResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }

    }
}
