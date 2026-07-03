using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{

    public override async Task<StockOperationResponse> IncreaseStock(IncreaseStockRequest request, ServerCallContext context)
    {

        //normalize et
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        //logla
        _logger.LogInformation(
          "IncreaseStock isteği alındı. SKU: {Sku}, Miktar: {Quantity}, Sebep: {Reason}",
          normalizedSku, request.Quantity, request.Reason);


        try
        {
            // Bu SKU için kilit al (eşzamanlı stok değişikliklerini önlemek için)

            await using var lockHandle = await _lockService.AcquireLocksAsync(
                new[] 
                { 
                    normalizedSku 
                },
                cancellationToken: context.CancellationToken);

            // MongoDB'den mevcut stok bilgisini getir
            var stock = await _repository.GetBySkuAsync(
                normalizedSku, context.CancellationToken);



            // SKU veritabanında yoksa hata dön
            if (stock is null)
            {
                _logger.LogWarning("IncreaseStock: SKU bulunamadı: {Sku}", normalizedSku);
                return new StockOperationResponse
                {
                    Success = false,
                    Message = $"SKU bulunamadı: {normalizedSku}"
                };
            }


            // İşlem öncesi değerleri kaydet
            var prevAvailable = stock.QuantityAvailable;
            var prevReserved = stock.QuantityReserved;

            // Stok miktarını artır
            stock.IncreaseStock(request.Quantity);

            using var session = await _dbContext.Client.StartSessionAsync(
               cancellationToken: context.CancellationToken);
            session.StartTransaction();

            try
            {
                // Güncellenmiş stoku veritabanına yaz
                await _repository.UpdateAsync(stock, session, context.CancellationToken);

                // Audit trail: Bu değişikliği transaction log'a kaydet
                var log = new InventoryTransactionLog(
                    sku: normalizedSku,
                    transactionType: InventoryTransactionType.Increase,
                    quantity: request.Quantity,
                    previousAvailable: prevAvailable,
                    newAvailable: stock.QuantityAvailable,
                    previousReserved: prevReserved,
                    newReserved: stock.QuantityReserved,
                    reason: request.Reason);
                await _repository.AddTransactionLogAsync(
                    log, session, context.CancellationToken);


                // Her şey başarılı → Transaction'ı onayla
                await session.CommitTransactionAsync(context.CancellationToken);

                _logger.LogInformation(
                    "IncreaseStock başarılı. SKU: {Sku}, Eski: {Old}, Yeni: {New}",
                    normalizedSku, prevAvailable, stock.QuantityAvailable);

                return new StockOperationResponse
                {
                    Success = true,
                    Message = $"Stok başarıyla artırıldı. Yeni miktar: {stock.QuantityAvailable}"
                };
            }
            catch (Exception ex)
            {
                // Hata olursa transaction'ı geri al
                await session.AbortTransactionAsync(context.CancellationToken);
                _logger.LogError(ex,
                    "IncreaseStock transaction hatası. SKU: {Sku}", normalizedSku);
                throw;
            }
        }
        catch (TimeoutException ex)
        {
            // Distributed Lock alınamadı
            _logger.LogError(ex,
                "IncreaseStock lock zaman aşımı. SKU: {Sku}", normalizedSku);
            return new StockOperationResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }


    }


}
