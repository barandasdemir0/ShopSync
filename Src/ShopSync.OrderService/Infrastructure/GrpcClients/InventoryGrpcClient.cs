using Grpc.Core;
using ShopSync.InventoryService.Protos;

namespace ShopSync.OrderService.Infrastructure.GrpcClients;

public sealed class InventoryGrpcClient : IInventoryGrpcClient
{
    private readonly InventoryGrpc.InventoryGrpcClient _client;
    private readonly ILogger<InventoryGrpcClient> _logger;
    public InventoryGrpcClient(
        InventoryGrpc.InventoryGrpcClient client,
        ILogger<InventoryGrpcClient> logger)
    {
        _client = client;
        _logger = logger;
    }


    public async Task<StockOperationResponse> ConfirmReservationAsync(string orderId, CancellationToken ct = default)
    {
        _logger.LogInformation(
             "InventoryService'e ConfirmReservation isteği gönderiliyor. OrderId: {OrderId}",
             orderId);
        try
        {
            var request = new ConfirmReservationRequest 
            { 
                OrderId = orderId 
            };
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
            throw;
        }
    }

    public async Task<ReleaseBatchResponse> ReleaseBatchAsync(string orderId, IEnumerable<ReservationItem> items, CancellationToken ct = default)
    {
        _logger.LogInformation(
           "InventoryService'e ReleaseBatch isteği gönderiliyor. OrderId: {OrderId}",
           orderId);
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
            throw;
        }
    }

    public async Task<ReserveBatchResponse> ReserveBatchAsync(string orderId, IEnumerable<ReservationItem> items, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "InventoryService'e ReserveBatch isteği gönderiliyor. OrderId: {OrderId}",
            orderId);

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
            throw;
        }
    }
}
