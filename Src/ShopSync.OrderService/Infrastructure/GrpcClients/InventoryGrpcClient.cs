using Grpc.Core;
using ShopSync.InventoryService.Protos;
using ShopSync.OrderService.Infrastructure.Telemetry;
using System.Diagnostics;

namespace ShopSync.OrderService.Infrastructure.GrpcClients;

public sealed class InventoryGrpcClient : IInventoryGrpcClient
{
    private readonly InventoryGrpc.InventoryGrpcClient _client;
    private readonly ILogger<InventoryGrpcClient> _logger;
    private readonly OrderMetrics _metrics;
    public InventoryGrpcClient(
        InventoryGrpc.InventoryGrpcClient client,
        ILogger<InventoryGrpcClient> logger,
        OrderMetrics metrics)
    {
        _client = client;
        _logger = logger;
        _metrics = metrics;
    }


    public async Task<StockOperationResponse> ConfirmReservationAsync(string orderId, IEnumerable<ReservationItem> items, CancellationToken ct = default)
    {
        _logger.LogInformation(
             "InventoryService'e ConfirmReservation isteği gönderiliyor. OrderId: {OrderId}",
             orderId);

        var sw = Stopwatch.StartNew(); // Süreyi ölçmeye başla

        try
        {
            var request = new ConfirmReservationRequest 
            { 
                OrderId = orderId 
            };
            request.Items.AddRange(items); 
            var response = await _client.ConfirmReservationAsync(request, cancellationToken: ct);
            if (!response.Success)
            {
                _logger.LogWarning(
                    "ConfirmReservation başarısız. OrderId: {OrderId}, Mesaj: {Message}",
                    orderId, response.Message);
            }
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "ConfirmReservation gRPC hatası. OrderId: {OrderId}, Status: {Status}",
                orderId, ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally
        {
            sw.Stop();
            _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds); // Süreyi kaydet
        }

    }



    public async Task<ReleaseBatchResponse> ReleaseBatchAsync(string orderId, IEnumerable<ReservationItem> items, CancellationToken ct = default)
    {
        _logger.LogInformation(
           "InventoryService'e ReleaseBatch isteği gönderiliyor. OrderId: {OrderId}",
           orderId);

        var sw = Stopwatch.StartNew();
        try
        {
            var request = new ReleaseBatchRequest 
            { 
                OrderId = orderId 
            };
            request.Items.AddRange(items);
            var response = await _client.ReleaseBatchAsync(request, cancellationToken: ct);
            if (!response.Success)
            {
                _logger.LogWarning(
                    "ReleaseBatch başarısız. OrderId: {OrderId}, Mesaj: {Message}",
                    orderId, response.Message);
            }
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "ReleaseBatch gRPC hatası. OrderId: {OrderId}, Status: {Status}",
                orderId, ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally
        {
            sw.Stop();
            _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds);
        }
    }

    public async Task<ReserveBatchResponse> ReserveBatchAsync(string orderId, IEnumerable<ReservationItem> items, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "InventoryService'e ReserveBatch isteği gönderiliyor. OrderId: {OrderId}",
            orderId);

        var sw = Stopwatch.StartNew();

        try
        {
            var request = new ReserveBatchRequest 
            { 
                OrderId = orderId 
            };

            request.Items.AddRange(items);

            var response = await _client.ReserveBatchAsync(request, cancellationToken: ct);

            if (!response.Success)
            {
                _logger.LogWarning(
                    "ReserveBatch başarısız. OrderId: {OrderId}, Mesaj: {Message}",
                    orderId, response.Message);
            }
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "ReserveBatch gRPC hatası. OrderId: {OrderId}, Status: {Status}",
                orderId, ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally
        {
            sw.Stop();
            _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds);
        }
    }

  

    // Ortak hata ayıklama metodu (Geçici hata mı, sistemsel mi?)
    private void RecordGrpcErrorMetrics(StatusCode statusCode)
    {
        var codeStr = statusCode.ToString();

        // Geçici (Transient) Hatalar: Network koptu, timeout oldu, server yoğun vs. Retry (tekrar deneme) yapılabilir.
        if (statusCode == StatusCode.Unavailable ||
            statusCode == StatusCode.DeadlineExceeded ||
            statusCode == StatusCode.Aborted ||
            statusCode == StatusCode.ResourceExhausted)
        {
            _metrics.GrpcTransientError(codeStr);
        }
        // Sistemsel Hatalar: Bug var, yetki yok, metot yok vs. Retry yapmak işe yaramaz.
        else
        {
            _metrics.GrpcSystemicError(codeStr);
        }
    }
}
