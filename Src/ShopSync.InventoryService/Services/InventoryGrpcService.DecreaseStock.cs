using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;


public sealed partial class InventoryGrpcService
{
    public override async Task<StockOperationResponse> DecreaseStock(DecreaseStockRequest request, ServerCallContext context)
    {
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        _logger.LogInformation(
           "DecreaseStock isteği alındı. SKU: {Sku}, Miktar: {Quantity}, Sebep: {Reason}",
           normalizedSku, request.Quantity, request.Reason);

        try
        {
            await using var lockHandle = await _lockService.AcquireLocksAsync(
              new[] 
              { 
                  normalizedSku 
              },
              cancellationToken: context.CancellationToken);

            var stock = await _repository.GetBySkuAsync(
                normalizedSku, context.CancellationToken);



            if (stock is null)
            {
                _logger.LogWarning("DecreaseStock: SKU bulunamadı: {Sku}", normalizedSku);
                return new StockOperationResponse
                {
                    Success = false,
                    Message = $"SKU bulunamadı: {normalizedSku}"
                };
            }

            //alttaki kodun amacı, stok miktarını azaltmadan önce mevcut stok ve rezerve miktarını kaydetmektir. Bu, işlem öncesi ve sonrası değerleri karşılaştırmak veya loglamak için kullanılabilir.
            var prevAvailable = stock.QuantityAvailable;
            var prevReserved = stock.QuantityReserved;

            // DDD metodu ile stok azalt
            stock.DecreaseStock(request.Quantity);

            using var session = await _dbContext.Client.StartSessionAsync(
              cancellationToken: context.CancellationToken);
            session.StartTransaction();


            try
            {
                await _repository.UpdateAsync(stock, session, context.CancellationToken);

                var log = new InventoryTransactionLog(
                   sku: normalizedSku,
                   transactionType: InventoryTransactionType.Decrease,
                   quantity: request.Quantity,
                   previousAvailable: prevAvailable,
                   newAvailable: stock.QuantityAvailable,
                   previousReserved: prevReserved,
                   newReserved: stock.QuantityReserved,
                   reason: request.Reason);
                await _repository.AddTransactionLogAsync(
                    log, session, context.CancellationToken);

                await session.CommitTransactionAsync(context.CancellationToken);
                _logger.LogInformation(
                    "DecreaseStock başarılı. SKU: {Sku}, Eski: {Old}, Yeni: {New}",
                    normalizedSku, prevAvailable, stock.QuantityAvailable);

                return new StockOperationResponse
                {
                    Success = true,
                    Message = $"Stok başarıyla azaltıldı. Yeni miktar: {stock.QuantityAvailable}"
                };
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync(context.CancellationToken);
                _logger.LogError(ex,
                    "DecreaseStock transaction hatası. SKU: {Sku}", normalizedSku);
                throw;
            }

        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex,
                "DecreaseStock lock zaman aşımı. SKU: {Sku}", normalizedSku);
            return new StockOperationResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }

    }
}
