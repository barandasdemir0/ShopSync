using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;
using StackExchange.Redis;


namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{

    // ReleaseBatch metodu, birden fazla ürünün rezervasyonlarını serbest bırakmak için kullanılır.
    public override async Task<ReleaseBatchResponse> ReleaseBatch(ReleaseBatchRequest request, ServerCallContext context)
    {
        var releaseGuardKey = $"release_guard:{request.OrderId}";
        var db = _redis.GetDatabase();

        // SET NX ile atomic kontrol: Bu orderId için daha önce release yapılmış mı?
        var isFirstRelease = await db.StringSetAsync(
            releaseGuardKey,
            DateTime.UtcNow.ToString("O"),
            TimeSpan.FromHours(24),
            When.NotExists);
        if (!isFirstRelease)
        {
            _logger.LogWarning(
                "Duplicate release engellendi! OrderId: {OrderId} için release zaten yapılmış.",
                request.OrderId);
            return new ReleaseBatchResponse
            {
                Success = true, // Idempotent (yinelenen) istek olduğu için True döner
                Message = $"Release zaten yapılmış. OrderId: {request.OrderId} (idempotent)"
            };
        }


        var alreadyCompleted = await _repository.IsOrderAlreadyCompletedAsync(
        request.OrderId, context.CancellationToken);
        if (alreadyCompleted)
        {
            _logger.LogWarning(
                "ReleaseBatch: Bu sipariş zaten işlenmiş. Duplicate release engellendi. OrderId: {OrderId}",
                request.OrderId);
            return new ReleaseBatchResponse
            {
                Success = false,
                Message = $"Bu sipariş zaten işlenmiş (release/confirm/expire). OrderId: {request.OrderId}"
            };
        }


        _logger.LogInformation(
            "ReleaseBatch isteği alındı. OrderId: {OrderId}, Ürün sayısı: {Count}",
            request.OrderId, request.Items.Count);



        // 1. İSTEKLERİ GRUPLA (AYNI SKU TEKRAR EDERSE MİKTARLARI TOPLA)
        var consolidatedItems = request.Items
            .GroupBy(i => i.Sku.Trim().ToUpperInvariant())
            .Select(g => new
            {
                NormalizedSku = g.Key,
                OriginalSku = g.First().Sku,
                TotalQuantity = g.Sum(i => i.Quantity)
            })
            .ToList();
        // kilitler ve DB için sadece benzersiz SKU'ları al
        var skus = consolidatedItems.Select(i => i.NormalizedSku).ToList();

        try
        {
            
            await using var lockHandle = await _lockService.AcquireLocksAsync(skus, cancellationToken: context.CancellationToken);
            var allInventoryItems = await _repository.GetBySkusAsync(skus, context.CancellationToken); // MongoDB'den stokları getir
            var releasePlan = new Dictionary<string, InventoryItem>(); // bu dictionary, hangi SKU'nun hangi stoktan release edileceğini tutacak
            var missingSkus = new List<string>(); // eksik SKU'ları tutacak
            var failedItems = new List<FailedItem>(); // release edilemeyen SKU'ları ve nedenlerini tutacak

            
            foreach (var item in consolidatedItems) 
            {
                var stocksForSku = allInventoryItems
                    .Where(i => i.Sku.Trim().ToUpperInvariant() == item.NormalizedSku)
                    .ToList(); // Bu SKU'ya ait tüm stokları al

                // Eğer stok yoksa, eksik SKU listesine ekle
                if (stocksForSku.Count == 0)
                {
                    missingSkus.Add(item.OriginalSku);
                    continue;
                }

                // HANGİ DEPODA REZERVE EDİLDİĞİNİ BUL (İptal edilecek kadar rezervasyonu olan depo)
                var suitableStock = stocksForSku.FirstOrDefault(s => s.CanRelease(item.TotalQuantity));
                if (suitableStock != null)
                {
                    releasePlan[item.NormalizedSku] = suitableStock;
                }
                else
                {
                    failedItems.Add(new FailedItem
                    {
                        Sku = item.OriginalSku,
                        AvailableQuantity = stocksForSku.Sum(s => s.QuantityAvailable),
                        RequestedQuantity = item.TotalQuantity,
                        Reason = "İptal edilecek kadar rezervasyon hiçbir depoda bulunamadı"
                    });
                }
            }
            // Eksik SKU Kontrolü
            if (missingSkus.Count > 0)
            {
                _logger.LogWarning("Release sırasında SKU'lar bulunamadı: {Skus}", string.Join(", ", missingSkus));
                return new ReleaseBatchResponse
                {
                    Success = false,
                    Message = $"Şu SKU'lar bulunamadı: {string.Join(", ", missingSkus)}"
                };
            }
            // Rezervasyon yeterliliği kontrolü
            if (failedItems.Count > 0)
            {
                _logger.LogWarning("ReleaseBatch başarısız. Yetersiz rezervasyon: {Count} ürün", failedItems.Count);
                return new ReleaseBatchResponse
                {
                    Success = false,
                    Message = "Bazı rezervasyonlar serbest bırakılamadı. Hiçbir işlem yapılmadı.",
                    FailedItems = { failedItems }
                };
            }
        

            using var session = await _dbContext.Client.StartSessionAsync(cancellationToken: context.CancellationToken);
            session.StartTransaction();

            try
            {
                foreach (var requestedItem in consolidatedItems)
                {

                    var stock = releasePlan[requestedItem.NormalizedSku];


                    var prevAvailable = stock.QuantityAvailable;
                    var prevReserved = stock.QuantityReserved;

                    // ddd ile güncellenmiş stok miktarlarını ayarla
                    stock.Release(requestedItem.TotalQuantity);



                    await _repository.UpdateAsync(
                    stock,
                    session,
                    context.CancellationToken);

                    var log = new InventoryTransactionLog(
                    sku: requestedItem.OriginalSku,
                    transactionType: InventoryTransactionType.Release,
                    quantity: requestedItem.TotalQuantity,
                    previousAvailable: prevAvailable,
                    newAvailable: stock.QuantityAvailable,
                    previousReserved: prevReserved,
                    newReserved: stock.QuantityReserved,
                    orderId: request.OrderId,
                    reason: "ReleaseBatch");

                    await _repository.AddTransactionLogAsync(
                    log,
                    session,
                    context.CancellationToken);
                }



                await session.CommitTransactionAsync(context.CancellationToken);



                _logger.LogInformation(
                    "ReleaseBatch başarılı. OrderId: {OrderId}", request.OrderId);
                return new ReleaseBatchResponse
                {
                    Success = true,
                    Message = "Tüm rezervasyonlar başarıyla serbest bırakıldı."
                };
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync(context.CancellationToken);
                _logger.LogError(ex,
                    "ReleaseBatch transaction hatası. OrderId: {OrderId}", request.OrderId);
                throw;
            }

        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex,
                "ReleaseBatch lock zaman aşımı. OrderId: {OrderId}", request.OrderId);
            return new ReleaseBatchResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }
       

    }

}
