using Grpc.Core;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    public override async Task<GetStockResponse> GetStock(GetStockRequest request, ServerCallContext context)
    {

        
        _logger.LogInformation("GetStock isteği: {Sku}", request.Sku); // gelen GetStock isteğinin SKU bilgisini logla.

        var item = await _repository.GetBySkuAsync(request.Sku,context.CancellationToken); // SKU'ya göre envanter öğesini veritabanından al.

        // Eğer öğe bulunamazsa, Found alanını false olarak ayarla ve miktarları 0 olarak döndür.
        if (item == null)
        {
            return new GetStockResponse
            {
                Sku = request.Sku,
                AvailableQuantity = 0,
                ReservedQuantity = 0,
                Found = false
            };
        }
        // Eğer öğe bulunursa, Found alanını true olarak ayarla ve miktarları döndür.
        return new GetStockResponse
        {
            Sku = item.Sku,
            AvailableQuantity = item.QuantityAvailable,
            ReservedQuantity = item.QuantityReserved,
            Found = true
        };
    }
}
