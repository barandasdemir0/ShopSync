using Grpc.Core;
using ShopSync.InventoryService.Protos;

namespace ShopSync.InventoryService.Services;

public sealed partial class InventoryGrpcService
{
    public async override Task<GetInventoryForecastResponse> GetInventoryForecast(GetInventoryForecastRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Forecasting isteği alındı. Sku: {Sku}, Days: {Days}", request.Sku, request.DaysToPredict);

		try
		{
           
            if (string.IsNullOrWhiteSpace(request.Sku) || request.DaysToPredict <= 0)
            {
                return new GetInventoryForecastResponse
                {
                    Success = false,
                    Message = "Geçersiz SKU veya tahminleme gün sayısı."
                };
            }

            // Son 30 günlük satış verisini al
            int historicalDays = 30;
            var sinceDate = DateTime.UtcNow.AddDays(-historicalDays);

            var logs = await _repository.GetTransactionLogsForSkuAsync(request.Sku.Trim().ToUpperInvariant(), sinceDate, context.CancellationToken);

            if (logs == null || logs.Count == 0)
            {
                return new GetInventoryForecastResponse
                {
                    Sku = request.Sku,
                    Success = true,
                    PredictedRequiredQuantity = 0,
                    Message = "Son 30 güne ait satış hareketi bulunmadığından gelecekteki gereksinim 0 olarak tahminlenmiştir."
                };
            }


            // Satış hızı (Günlük Ortalama) = Son 30 günde satılan toplam / 30
            long totalSoldIn30Days = logs.Sum(x => x.Quantity);
            double dailyAverage = (double)totalSoldIn30Days / historicalDays;

            // Tahmin edilen stok miktarı = Günlük ortalama satış * İstenen gelecek gün sayısı
            int predictedRequirement = (int)Math.Ceiling(dailyAverage * request.DaysToPredict);

            return new GetInventoryForecastResponse
            {
                Sku = request.Sku,
                Success = true,
                PredictedRequiredQuantity = predictedRequirement,
                Message = $"Son {historicalDays} günlük geçmiş hareketlere göre, önümüzdeki {request.DaysToPredict} gün için ortalama {predictedRequirement} adet stoka ihtiyaç duyulacaktır."
            };

        }
		catch (Exception ex)
		{

            _logger.LogError(ex, "Forecasting hesaplanırken bir hata oluştu. Sku: {Sku}", request.Sku);
            return new GetInventoryForecastResponse
            {
                Success = false,
                Message = "Tahminleme işlemi sırasında sistemsel bir hata oluştu."
            };
        }
    }
}
