using Grpc.Core;
using MongoDB.Driver;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    public override async Task<StockOperationResponse> DeleteInventoryItem(DeleteInventoryItemRequest request, ServerCallContext context)
    {
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        string normalizedWarehouse;
  

        if (string.IsNullOrWhiteSpace(request.WarehouseCode))
        {
            normalizedWarehouse = "DEFAULT";
        }
        else
        {
            normalizedWarehouse = request.WarehouseCode.Trim().ToUpperInvariant();
        }

        _logger.LogWarning(
            "DeleteInventoryItem isteği alındı. SKU: {Sku}, Depo: {Warehouse}. Bu kayıt kalıcı olarak silinecek.",
            normalizedSku,
            normalizedWarehouse);

        try
        {
            var lockKeys = new List<string>
            {
                normalizedSku
            };

            await using var lockHandle = await _lockService.AcquireLocksAsync(
                lockKeys,
                cancellationToken: context.CancellationToken);

            var stock = await _repository.GetBySkuAndWarehouseAsync(
                normalizedSku,
                normalizedWarehouse,
                context.CancellationToken);

            if (stock is null)
            {
                return new StockOperationResponse
                {
                    Success = false,
                    Message = $"Silinecek ürün bulunamadı. SKU: {normalizedSku}, Depo: {normalizedWarehouse}"
                };
            }

            if (stock.QuantityReserved > 0)
            {
                return new StockOperationResponse
                {
                    Success = false,
                    Message = $"Ürün silinemez. Üzerinde aktif rezervasyon var. Reserved: {stock.QuantityReserved}"
                };
            }

            using var session = await _dbContext.Client.StartSessionAsync(
                cancellationToken: context.CancellationToken);

            session.StartTransaction();

            try
            {
                var skuFilter = Builders<InventoryItem>.Filter.Eq(
                    x => x.Sku,
                    normalizedSku);

                var warehouseFilter = Builders<InventoryItem>.Filter.Eq(
                    x => x.WarehouseCode,
                    normalizedWarehouse);

                var deleteFilter = Builders<InventoryItem>.Filter.And(
                    skuFilter,
                    warehouseFilter);

                var deleteResult = await _dbContext.InventoryItems.DeleteOneAsync(
                    session,
                    deleteFilter,
                    cancellationToken: context.CancellationToken);

                if (deleteResult.DeletedCount == 0)
                {
                    await session.AbortTransactionAsync(
                        context.CancellationToken);

                    return new StockOperationResponse
                    {
                        Success = false,
                        Message = "Silme işlemi başarısız. Kayıt bulunamadı veya silinemedi."
                    };
                }

                var logQuantity = stock.QuantityAvailable;
                // Stok 0 ise, log tablosunun hata vermemesi için miktarı 1 yapıyoruz
                if (logQuantity == 0)
                {
                    logQuantity = 1;
                }

                var transactionLog = new InventoryTransactionLog(
                    sku: normalizedSku,
                    transactionType: InventoryTransactionType.Decrease,
                     quantity: logQuantity, 
                    previousAvailable: stock.QuantityAvailable,
                    newAvailable: 0,
                    previousReserved: stock.QuantityReserved,
                    newReserved: 0,
                    reason: $"Ürün envanterden silindi. Depo: {normalizedWarehouse}");

                await _repository.AddTransactionLogAsync(
                    transactionLog,
                    session,
                    context.CancellationToken);

                await session.CommitTransactionAsync(
                    context.CancellationToken);

                _logger.LogInformation(
                    "Envanter kalemi başarıyla silindi. SKU: {Sku}, Depo: {Warehouse}",
                    normalizedSku,
                    normalizedWarehouse);

                return new StockOperationResponse
                {
                    Success = true,
                    Message = $"Ürün başarıyla silindi. SKU: {normalizedSku}, Depo: {normalizedWarehouse}"
                };
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync(
                    context.CancellationToken);

                _logger.LogError(
                    ex,
                    "DeleteInventoryItem transaction hatası. SKU: {Sku}, Depo: {Warehouse}",
                    normalizedSku,
                    normalizedWarehouse);

                throw;
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(
                ex,
                "DeleteInventoryItem lock zaman aşımı. SKU: {Sku}, Depo: {Warehouse}",
                normalizedSku,
                normalizedWarehouse);

            return new StockOperationResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }
    }
}
