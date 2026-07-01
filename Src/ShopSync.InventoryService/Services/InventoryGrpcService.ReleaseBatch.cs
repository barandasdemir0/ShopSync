using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;


namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{

    // ReleaseBatch metodu, birden fazla ürünün rezervasyonlarını serbest bırakmak için kullanılır.
    public override async Task<ReleaseBatchResponse> ReleaseBatch(ReleaseBatchRequest request, ServerCallContext context)
    {
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
            //SKU'lar için kilitleri al (aynı SKU üzerinde eşzamanlı işlemleri önlemek için)
            await using var lockHandle = await _lockService.AcquireLocksAsync(skus,cancellationToken:context.CancellationToken);
            var inventoryItems = await _repository.GetBySkusAsync(skus,context.CancellationToken); // SKU'ları veritabanından al
            var inventoryMap = inventoryItems.ToDictionary(i => i.Sku.Trim().ToUpperInvariant()); // SKU'ları normalize ederek bir dictionary oluştur key label eşitliği için


            // 3. Eksik SKU Kontrolü
            var missingSkus = skus.Where(s => !inventoryMap.ContainsKey(s)).ToList();
            if (missingSkus.Count > 0)
            {
                _logger.LogWarning("Release sırasında SKU'lar bulunamadı: {Skus}", string.Join(", ", missingSkus));
                return new ReleaseBatchResponse
                {
                    Success = false,
                    Message = $"Şu SKU'lar bulunamadı: {string.Join(", ", missingSkus)}"
                };
            }
            // 4. Rezervasyon yeterliliği kontrolü
            var failedItems = consolidatedItems
                .Where(item => !inventoryMap[item.NormalizedSku].CanRelease(item.TotalQuantity))
                .Select(item => new FailedItem
                {
                    Sku = item.OriginalSku,
                    AvailableQuantity = inventoryMap[item.NormalizedSku].QuantityAvailable, // mevcut stok miktarı
                    RequestedQuantity = item.TotalQuantity,
                    Reason = "Serbest bırakılacak kadar rezervasyon yok"
                })
                .ToList();
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

                    var stock = inventoryMap[requestedItem.NormalizedSku];


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
