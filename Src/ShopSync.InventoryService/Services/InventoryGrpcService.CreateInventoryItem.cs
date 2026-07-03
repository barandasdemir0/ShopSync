using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    public override async Task<StockOperationResponse> CreateInventoryItem(CreateInventoryItemRequest request, ServerCallContext context)
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

        // Varsayılan düşük stok eşiği 10 olarak ayarlanmıştır.
        var lowStockThreshold = request.LowStockThreshold;

        if (lowStockThreshold <= 0)
        {
            lowStockThreshold = 10;
        }

        _logger.LogInformation(
            "CreateInventoryItem isteği alındı. SKU: {Sku}, Depo: {Warehouse}, Başlangıç Stoğu: {Qty}",
            normalizedSku,
            normalizedWarehouse,
            request.InitialQuantity);

        try
        {
            var lockKeys = new List<string>
            {
            normalizedSku
            };

            await using var lockHandle = await _lockService.AcquireLocksAsync(
                lockKeys,
                cancellationToken: context.CancellationToken);

            var existingItem = await _repository.GetBySkuAndWarehouseAsync(
                normalizedSku,
                normalizedWarehouse,
                context.CancellationToken);

            if (existingItem is not null)
            {
                return new StockOperationResponse
                {
                    Success = false,
                    Message = $"Ürün ({normalizedSku}) zaten '{normalizedWarehouse}' deposunda mevcut!"
                };
            }

            var newItem = new InventoryItem(
                sku: normalizedSku,
                quantityAvailable: request.InitialQuantity,
                warehouseCode: normalizedWarehouse,
                lowStockThreshold: lowStockThreshold);

            using var session = await _dbContext.Client.StartSessionAsync(
                cancellationToken: context.CancellationToken);

            session.StartTransaction();

            try
            {
                await _repository.InsertAsync(
                    newItem,
                    session,
                    context.CancellationToken);

                var transactionLog = new InventoryTransactionLog(
                    sku: normalizedSku,
                    transactionType: InventoryTransactionType.Increase,
                    quantity: request.InitialQuantity,
                    previousAvailable: 0,
                    newAvailable: request.InitialQuantity,
                    previousReserved: 0,
                    newReserved: 0,
                    reason: $"Ürün sisteme ilk defa eklendi. Depo: {normalizedWarehouse}");

                await _repository.AddTransactionLogAsync(
                    transactionLog,
                    session,
                    context.CancellationToken);

                await session.CommitTransactionAsync(
                    context.CancellationToken);

                _logger.LogInformation(
                    "Yeni envanter kalemi oluşturuldu. SKU: {Sku}, Depo: {Warehouse}",
                    normalizedSku,
                    normalizedWarehouse);

                return new StockOperationResponse
                {
                    Success = true,
                    Message = $"Ürün başarıyla oluşturuldu. SKU: {normalizedSku}, Depo: {normalizedWarehouse}, Stok: {request.InitialQuantity}"
                };
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync(
                    context.CancellationToken);

                _logger.LogError(
                    ex,
                    "CreateInventoryItem transaction hatası. SKU: {Sku}, Depo: {Warehouse}",
                    normalizedSku,
                    normalizedWarehouse);

                throw;
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(
                ex,
                "CreateInventoryItem lock zaman aşımı. SKU: {Sku}, Depo: {Warehouse}",
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
