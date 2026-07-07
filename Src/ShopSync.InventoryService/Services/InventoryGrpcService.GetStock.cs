using Grpc.Core;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    public override async Task<GetStockResponse> GetStock(GetStockRequest request, ServerCallContext context)
    {

        
        _logger.LogInformation("GetStock isteği: {Sku}", request.Sku); // gelen GetStock isteğinin SKU bilgisini logla.

        // Sadece ilk depoyu değil, bu SKU'nun olduğu TÜM depoları (Liste halinde) getiriyoruz
        var items = await _repository.GetBySkuAllWarehousesAsync(request.Sku, context.CancellationToken);


        // Eğer öğe bulunamazsa, Found alanını false olarak ayarla ve miktarları 0 olarak döndür.
        if (items == null || items.Count == 0)
        {
            return new GetStockResponse
            {
                Sku = request.Sku,
                AvailableQuantity = 0,
                ReservedQuantity = 0,
                Found = false
            };
        }

        // Tüm depolardaki mevcut ve rezerve stokları topluyoruz
        var totalAvailable = items.Sum(x => x.QuantityAvailable);
        var totalReserved = items.Sum(x => x.QuantityReserved);

        // Toplam stok miktarını dönüyoruz
        return new GetStockResponse
        {
            Sku = request.Sku,
            AvailableQuantity = totalAvailable,
            ReservedQuantity = totalReserved,
            Found = true
        };
    }
}
