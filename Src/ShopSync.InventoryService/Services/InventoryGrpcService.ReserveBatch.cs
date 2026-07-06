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
            var allInventoryItems = await _repository.GetBySkusAsync(skus, context.CancellationToken);

            var reservationPlan = new Dictionary<string, InventoryItem>();
            var missingSkus = new List<string>();
            var failedItems = new List<FailedItem>();

            foreach (var item in consolidatedItems)
            {

                // Bu SKU'ya ait tüm depo kayıtları
                var stocksForSku = allInventoryItems
                    .Where(i => i.Sku.Trim().ToUpperInvariant() == item.NormalizedSku)
                    .ToList();

                if (stocksForSku.Count == 0)
                {
                    missingSkus.Add(item.OriginalSku);
                    continue;
                }
                // Yeterli stoğu olan HERHANGİ BİR depoyu bul
                var suitableStock = stocksForSku.FirstOrDefault(s => s.CanReserve(item.TotalQuantity));
                if (suitableStock != null)
                {
                    // Bulunan depoyu plana ekle
                    reservationPlan[item.NormalizedSku] = suitableStock;
                }
                else
                {
                    // Hiçbir depoda yeterli stok yoksa hata listesine ekle
                    failedItems.Add(new FailedItem
                    {
                        Sku = item.OriginalSku,
                        AvailableQuantity = stocksForSku.Sum(s => s.QuantityAvailable), // Tüm depolardaki toplamı göster
                        RequestedQuantity = item.TotalQuantity,
                        Reason = "Hiçbir depoda yeterli stok yok"
                    });
                }

            }

            // Önce eksik SKU'ları kontrol et
            if (missingSkus.Count > 0)
            {
                _logger.LogWarning("ReserveBatch başarısız. Bulunamayan SKU'lar: {Skus}", string.Join(", ", missingSkus));
                _metrics.ReservationsFailed.Add(1);
                return new ReserveBatchResponse
                {
                    Success = false,
                    Message = $"Şu SKU'lar bulunamadı: {string.Join(", ", missingSkus)}"
                };
            }


            // Stok yeterliliği kontrolü 
            if (failedItems.Count > 0)
            {
                _logger.LogWarning("ReserveBatch başarısız. Yetersiz stok: {Count} ürün", failedItems.Count);
                _metrics.ReservationsFailed.Add(1);
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

                    var stock = reservationPlan[requestedItem.NormalizedSku];



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
                _metrics.ReservationsCompleted.Add(1);

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
            _metrics.ReservationsFailed.Add(1);
            // Distributed Lock alınamadı (tüm denemeler tükendi)
            _logger.LogError(ex,
                "ReserveBatch lock zaman aşımı. OrderId: {OrderId}", request.OrderId);
            return new ReserveBatchResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }
      
    }
}
