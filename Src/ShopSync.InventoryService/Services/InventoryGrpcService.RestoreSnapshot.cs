using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    public override async Task<StockOperationResponse> RestoreSnapshot(RestoreSnapshotRequest request, ServerCallContext context)
    {
        _logger.LogWarning(
          "RestoreSnapshot isteği alındı! SnapshotId: {Id}. " +
          "Bu işlem TÜM STOKLARI snapshot değerleriyle DEĞİŞTİRECEK!",
          request.SnapshotId);


        // Snapshot'ı getir
        var snapshot = await _repository.GetSnapshotByIdAsync(
            request.SnapshotId, context.CancellationToken);
        if (snapshot is null)
        {
            return new StockOperationResponse
            {
                Success = false,
                Message = $"Snapshot bulunamadı: {request.SnapshotId}"
            };
        }

        // Tüm SKU'ları kilitle (tehlikeli operasyon, tüm stoklar etkilenir)
        var allSkus = snapshot.Items.Select(i => i.Sku).Distinct().ToList();


        try
        {
            // Tüm SKU'lar için kilit al
            await using var lockHandle = await _lockService.AcquireLocksAsync(
               allSkus, cancellationToken: context.CancellationToken);

            // MongoDB transaction başlat
            using var session = await _dbContext.Client.StartSessionAsync(
                cancellationToken: context.CancellationToken);

            // Transaction başlat
            session.StartTransaction();

            try
            {
                var restoredCount = 0;

                foreach (var snapshotItem in snapshot.Items)
                {
                    var current = await _repository.GetBySkuAndWarehouseAsync(
                       snapshotItem.Sku, snapshotItem.WarehouseCode,
                       context.CancellationToken);
                    if (current is null)
                    {
                        _logger.LogWarning(
                            "Restore: SKU/Warehouse bulunamadı, atlanıyor. SKU: {Sku}, Warehouse: {Wh}",
                            snapshotItem.Sku, snapshotItem.WarehouseCode);
                        continue;
                    }

                    // Önceki değerleri al
                    var prevAvailable = current.QuantityAvailable;
                    var prevReserved = current.QuantityReserved;


                    // Mevcut ve Rezerve stokları snapshot değerleriyle birebir eziyoruz (güncelliyoruz)
                    current.RestoreSnapshotState(snapshotItem.QuantityAvailable, snapshotItem.QuantityReserved);

                    // Aşağıdaki log işlemleri için availableDiff'i hesaplıyoruz
                    var availableDiff = snapshotItem.QuantityAvailable - prevAvailable;


                    await _repository.UpdateAsync(current, session, context.CancellationToken);

                    // 1. Miktar hesaplamasını nesne dışında, açık ve net bir şekilde yapıyoruz
                    int absoluteDiff = Math.Abs(availableDiff);
                    int logQuantity;


                    // Eğer fark 0 ise, log için 1 olarak ayarlıyoruz, aksi takdirde farkı kullanıyoruz
                    if (absoluteDiff == 0)
                    {
                        logQuantity = 1;
                    }
                    // Eğer fark 0 değilse, log için farkı kullanıyoruz
                    else
                    {
                        logQuantity = absoluteDiff;
                    }

                    // 2. Audit log nesnesini çok daha temiz bir şekilde oluşturuyoruz
                    var log = new InventoryTransactionLog(
                        sku: snapshotItem.Sku,
                        transactionType: InventoryTransactionType.Rebalance,
                        quantity: logQuantity,
                        previousAvailable: prevAvailable,
                        newAvailable: current.QuantityAvailable,
                        previousReserved: prevReserved,
                        newReserved: current.QuantityReserved,
                        reason: $"RestoreSnapshot (ID: {request.SnapshotId})");

                    await _repository.AddTransactionLogAsync(log, session, context.CancellationToken);
                    restoredCount++;




                }

                await session.CommitTransactionAsync(context.CancellationToken);
                _logger.LogWarning(
                    "RestoreSnapshot tamamlandı. SnapshotId: {Id}, Geri yüklenen: {Count} ürün",
                    request.SnapshotId, restoredCount);
                return new StockOperationResponse
                {
                    Success = true,
                    Message = $"Snapshot başarıyla geri yüklendi. {restoredCount} ürün güncellendi."
                };






            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync(context.CancellationToken);
                _logger.LogError(ex,
                    "RestoreSnapshot transaction hatası. SnapshotId: {Id}",
                    request.SnapshotId);
                throw;
            }









        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex,
                "RestoreSnapshot lock zaman aşımı. SnapshotId: {Id}",
                request.SnapshotId);
            return new StockOperationResponse
            {
                Success = false,
                Message = $"Kilit alınamadı: {ex.Message}"
            };
        }


    }
}
