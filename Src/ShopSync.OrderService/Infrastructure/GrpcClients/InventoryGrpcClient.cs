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

    public async Task<StockOperationResponse> CreateInventoryItemAsync(string sku, int initialQuantity, string warehouseCode, int lowStockThreshold, CancellationToken ct = default)
    {
        _logger.LogInformation("InventoryService'e CreateInventoryItem isteği gönderiliyor. SKU: {Sku}", sku);
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new CreateInventoryItemRequest 
            { 
                Sku = sku, 
                InitialQuantity = initialQuantity, 
                WarehouseCode = warehouseCode,
                LowStockThreshold = lowStockThreshold 
            };
            var response = await _client.CreateInventoryItemAsync(request, cancellationToken: ct);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "CreateInventoryItem gRPC hatası. SKU: {Sku}, Status: {Status}", sku, ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally 
        { 
            sw.Stop(); 
            _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds);
        }
    }

    public async Task<CreateSnapshotResponse> CreateSnapshotAsync(string description, CancellationToken ct = default)
    {
        _logger.LogInformation("InventoryService'e CreateSnapshot isteği gönderiliyor. Açıklama: {Description}", description);
        // Stopwatch ile süreyi ölçmeye başla
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new CreateSnapshotRequest 
            { 
                Description = description 
            };
            var response = await _client.CreateSnapshotAsync(request, cancellationToken: ct);
            if (!response.Success)
            {
                _logger.LogWarning("CreateSnapshot başarısız. Mesaj: {Message}", response.Message);
            }
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "CreateSnapshot gRPC hatası. Status: {Status}", ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally
        {
            sw.Stop();
            _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds);
        }
    }


    //bu metot, stok azaltma işlemi için gRPC çağrısı yapar ve hata durumlarını loglar.
    public async Task<StockOperationResponse> DecreaseStockAsync(string sku, int quantity, string reason, string warehouseCode, CancellationToken ct = default)
    {
        _logger.LogInformation("InventoryService'e DecreaseStock isteği gönderiliyor. SKU: {Sku}", sku);
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new DecreaseStockRequest 
            { 
                Sku = sku, 
                Quantity = quantity, 
                Reason = reason ,
                //WarehouseCode = warehouseCode
            };
            var response = await _client.DecreaseStockAsync(request, cancellationToken: ct);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "DecreaseStock gRPC hatası. SKU: {Sku}, Status: {Status}", sku, ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally 
        { 
            sw.Stop();
            _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds); 
        }
    }

    public async Task<StockOperationResponse> DeleteInventoryItemAsync(string sku, string warehouseCode, CancellationToken ct = default)
    {
        _logger.LogInformation("InventoryService'e DeleteInventoryItem isteği gönderiliyor. SKU: {Sku}", sku);
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new DeleteInventoryItemRequest 
            { 
                Sku = sku, 
                WarehouseCode = warehouseCode 
            };
            var response = await _client.DeleteInventoryItemAsync(request, cancellationToken: ct);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "DeleteInventoryItem gRPC hatası. SKU: {Sku}, Status: {Status}", sku, ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally 
        { 
            sw.Stop(); 
            _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds);  //elapsed milliseconds: geçen süreyi milisaniye cinsinden kaydet
        }
    }

    public async Task<GetStockResponse> GetStockAsync(string sku, CancellationToken ct = default)
    {
        _logger.LogInformation("InventoryService'e GetStock isteği gönderiliyor. SKU: {Sku}", sku);
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new GetStockRequest 
            { 
                Sku = sku 
            };
            var response = await _client.GetStockAsync(request, cancellationToken: ct);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "GetStock gRPC hatası. SKU: {Sku}, Status: {Status}", sku, ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally
        {
            sw.Stop();
            _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds);
        }
    }

    public async Task<StockOperationResponse> IncreaseStockAsync(string sku, int quantity, string reason, string warehouseCode, CancellationToken ct = default)
    {
        _logger.LogInformation("InventoryService'e IncreaseStock isteği gönderiliyor. SKU: {Sku}", sku);
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new IncreaseStockRequest
            {
                Sku = sku,
                Quantity = quantity,
                Reason = reason,
               // WarehouseCode = warehouseCode
            };
            var response = await _client.IncreaseStockAsync(request, cancellationToken: ct);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "IncreaseStock gRPC hatası. SKU: {Sku}, Status: {Status}", sku, ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally 
        { 
            sw.Stop();
            _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds); 
        }
    }

    //bu metot, stok dengeleme işlemi için gRPC çağrısı yapar ve hata durumlarını loglar.
    public async Task<StockOperationResponse> RebalanceStockAsync(string sku, int quantity, string fromLocation, string toLocation, string reason, CancellationToken ct = default)
    {
        _logger.LogInformation("InventoryService'e RebalanceStock isteği gönderiliyor. SKU: {Sku}", sku);
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new RebalanceStockRequest 
            { 
                Sku = sku, 
                Quantity = quantity, 
                FromLocation = fromLocation, 
                ToLocation = toLocation, 
                Reason = reason 
            };
            var response = await _client.RebalanceStockAsync(request, cancellationToken: ct);
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "RebalanceStock gRPC hatası. SKU: {Sku}, Status: {Status}", sku, ex.StatusCode);
            RecordGrpcErrorMetrics(ex.StatusCode);
            throw;
        }
        finally 
        { 
            sw.Stop(); _metrics.RecordGrpcCallDuration(sw.ElapsedMilliseconds); 
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

    public async Task<StockOperationResponse> RestoreSnapshotAsync(string snapshotId, CancellationToken ct = default)
    {
        _logger.LogInformation("InventoryService'e RestoreSnapshot isteği gönderiliyor. SnapshotId: {SnapshotId}", snapshotId);
        var sw = Stopwatch.StartNew();
        try
        {
            var request = new RestoreSnapshotRequest { SnapshotId = snapshotId };
            var response = await _client.RestoreSnapshotAsync(request, cancellationToken: ct);
            if (!response.Success)
            {
                _logger.LogWarning("RestoreSnapshot başarısız. SnapshotId: {SnapshotId}, Mesaj: {Message}", snapshotId, response.Message);
            }
            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "RestoreSnapshot gRPC hatası. SnapshotId: {SnapshotId}, Status: {Status}", snapshotId, ex.StatusCode);
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
