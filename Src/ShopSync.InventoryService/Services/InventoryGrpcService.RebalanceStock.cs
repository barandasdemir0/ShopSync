using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    public async override Task<StockOperationResponse> RebalanceStock(RebalanceStockRequest request, ServerCallContext context)
    {
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();
        var sourceWarehouse = request.FromLocation.Trim().ToUpperInvariant();
        var targetWarehouse = request.ToLocation.Trim().ToUpperInvariant();


        _logger.LogInformation(
            "RebalanceStock isteği. SKU: {Sku}, Miktar: {Qty}, " +
            "Kaynak: {Source} -> Hedef: {Target}, Sebep: {Reason}",
            normalizedSku, request.Quantity,
            sourceWarehouse, targetWarehouse, request.Reason);



        // Aynı depoya transfer anlamsızdır
        if (sourceWarehouse == targetWarehouse)
        {
            return new StockOperationResponse
            {
                Success = false,
                Message = "Kaynak ve hedef depo aynı olamaz."
            };
        }

        try
        {
            // Her iki deponun SKU'su için kilit al.
            // Lock key'leri: "SKU:WAREHOUSE" formatında, alfabetik sırayla
            var lockKeys = new[] 
            { 
                $"{normalizedSku}:{sourceWarehouse}", 
                $"{normalizedSku}:{targetWarehouse}" 
            };

            await using var lockHandle = await _lockService.AcquireLocksAsync(
              lockKeys, cancellationToken: context.CancellationToken);

            // Kaynak ve hedef depoyu veritabanından getir
            var sourceStock = await _repository.GetBySkuAndWarehouseAsync(
                normalizedSku, sourceWarehouse, context.CancellationToken);

            var targetStock = await _repository.GetBySkuAndWarehouseAsync(
                normalizedSku, targetWarehouse, context.CancellationToken);


            if (sourceStock is null)
            {
                return new StockOperationResponse
                {
                    Success = false,
                    Message = $"Kaynak depoda ({sourceWarehouse}) SKU bulunamadı: {normalizedSku}"
                };
            }
            if (targetStock is null)
            {
                return new StockOperationResponse
                {
                    Success = false,
                    Message = $"Hedef depoda ({targetWarehouse}) SKU bulunamadı: {normalizedSku}"
                };
            }


            // İşlem öncesi değerleri kaydet (audit trail için)
            var sourcePrevAvailable = sourceStock.QuantityAvailable;
            var targetPrevAvailable = targetStock.QuantityAvailable;


            sourceStock.DecreaseStock(request.Quantity);
            targetStock.IncreaseStock(request.Quantity);

            using var session = await _dbContext.Client.StartSessionAsync(cancellationToken: context.CancellationToken);
            session.StartTransaction();

            try
            {
                //kaynak ve hedef stokları güncelle
                await _repository.UpdateAsync(sourceStock, session, context.CancellationToken);

                await _repository.UpdateAsync(targetStock, session, context.CancellationToken);

                // Kaynak depo için audit log (stok azaldı)
                var sourceLog = new InventoryTransactionLog(
                    sku: normalizedSku,
                    transactionType: InventoryTransactionType.Rebalance,
                    quantity: request.Quantity,
                    previousAvailable: sourcePrevAvailable,
                    newAvailable: sourceStock.QuantityAvailable,
                    previousReserved: sourceStock.QuantityReserved,
                    newReserved: sourceStock.QuantityReserved,
                    reason: $"Rebalance: {sourceWarehouse}  {targetWarehouse}. {request.Reason}");
                await _repository.AddTransactionLogAsync(sourceLog, session, context.CancellationToken);

                // Hedef depo için audit log (stok arttı)
                var targetLog = new InventoryTransactionLog(
                    sku: normalizedSku,
                    transactionType: InventoryTransactionType.Rebalance,
                    quantity: request.Quantity,
                    previousAvailable: targetPrevAvailable,
                    newAvailable: targetStock.QuantityAvailable,
                    previousReserved: targetStock.QuantityReserved,
                    newReserved: targetStock.QuantityReserved,
                    reason: $"Rebalance: {sourceWarehouse}  {targetWarehouse}. {request.Reason}");
                await _repository.AddTransactionLogAsync(targetLog, session, context.CancellationToken);


                await session.CommitTransactionAsync(context.CancellationToken);

                _logger.LogInformation(
                    "RebalanceStock başarılı. SKU: {Sku}, {Source}: {OldS}→{NewS}, {Target}: {OldT}→{NewT}",
                    normalizedSku,
                    sourceWarehouse, sourcePrevAvailable, sourceStock.QuantityAvailable,
                    targetWarehouse, targetPrevAvailable, targetStock.QuantityAvailable);

                return new StockOperationResponse
                {
                    Success = true,
                    Message = $"Transfer başarılı. {sourceWarehouse}: {sourceStock.QuantityAvailable}, {targetWarehouse}: {targetStock.QuantityAvailable}"
                };

            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync(context.CancellationToken);
                _logger.LogError(ex, "RebalanceStock transaction hatası. SKU: {Sku}", normalizedSku);
                throw;
            }

        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "RebalanceStock lock zaman aşımı. SKU: {Sku}", normalizedSku);
            return new StockOperationResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }




    }
}

