using Grpc.Core;
using ShopSync.InventoryService.Models;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    public override async Task<CreateSnapshotResponse> CreateSnapshot(CreateSnapshotRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateSnapshot isteği alındı. Açıklama: {Desc}",
            request.Description);


        // Tüm mevcut stok öğelerini getir
        var allItems = await _repository.GetAllItemsAsync(context.CancellationToken);


        // Her bir InventoryItem'ı SnapshotItem'a dönüştür
        var snapshotItems = allItems.Select(item => new SnapshotItem
        {
            Sku = item.Sku,
            WarehouseCode = item.WarehouseCode,
            QuantityAvailable = item.QuantityAvailable,
            QuantityReserved = item.QuantityReserved,
            LowStockThreshold = item.LowStockThreshold
        }).ToList();


        // Snapshot oluştur ve kaydet
        var snapshot = new InventorySnapshot(request.Description, snapshotItems);
        await _repository.InsertSnapshotAsync(snapshot, context.CancellationToken);
        _logger.LogInformation(
            "Snapshot başarıyla oluşturuldu. ID: {Id}, Ürün sayısı: {Count}",
            snapshot.Id, snapshot.ItemCount);
        return new CreateSnapshotResponse
        {
            Success = true,
            SnapshotId = snapshot.Id,
            Message = $"Snapshot başarıyla oluşturuldu.",
            TotalItems = snapshot.ItemCount
        };



    }
}
