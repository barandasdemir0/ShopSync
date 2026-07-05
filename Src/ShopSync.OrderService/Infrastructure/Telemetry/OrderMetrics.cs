using System.Diagnostics.Metrics;

namespace ShopSync.OrderService.Infrastructure.Telemetry;

public sealed class OrderMetrics
{
    // Sipariş oluşturma sayacı (durum bazlı etiketlerle)
    private readonly Counter<long> _ordersCreated;
    // Sipariş onaylama sayacı
    private readonly Counter<long> _ordersConfirmed;
    // Sipariş iptal sayacı
    private readonly Counter<long> _ordersCancelled;
    // Sipariş expire sayacı
    private readonly Counter<long> _ordersExpired;
    // Dead letter queue sayacı
    private readonly Counter<long> _deadLetterCount;
    // Stok rezervasyon süresi histogramı
    private readonly Histogram<double> _reservationDurationMs;
    // Order-to-confirm süresi histogramı
    private readonly Histogram<double> _confirmationDurationMs;
    // gRPC çağrı süresi histogramı
    private readonly Histogram<double> _grpcCallDurationMs;
    // Circuit breaker durum değişikliği sayacı
    private readonly Counter<long> _circuitBreakerStateChanges;
    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("ShopSync.OrderService");
        _ordersCreated = meter.CreateCounter<long>(
            "shopsync_orders_created_total",
            description: "Oluşturulan toplam sipariş sayısı");
        _ordersConfirmed = meter.CreateCounter<long>(
            "shopsync_orders_confirmed_total",
            description: "Onaylanan toplam sipariş sayısı");
        _ordersCancelled = meter.CreateCounter<long>(
            "shopsync_orders_cancelled_total",
            description: "İptal edilen toplam sipariş sayısı");
        _ordersExpired = meter.CreateCounter<long>(
            "shopsync_orders_expired_total",
            description: "Süresi dolan toplam sipariş sayısı");
        _deadLetterCount = meter.CreateCounter<long>(
            "shopsync_dead_letter_total",
            description: "Dead letter queue'ya düşen toplam sipariş sayısı");
        _reservationDurationMs = meter.CreateHistogram<double>(
            "shopsync_reservation_duration_ms",
            unit: "ms",
            description: "Stok rezervasyon süresi (milisaniye)");
        _confirmationDurationMs = meter.CreateHistogram<double>(
            "shopsync_confirmation_duration_ms",
            unit: "ms",
            description: "Sipariş oluşturma-onay süresi (milisaniye)");
        _grpcCallDurationMs = meter.CreateHistogram<double>(
            "shopsync_grpc_call_duration_ms",
            unit: "ms",
            description: "gRPC çağrı süresi (milisaniye)");
        _circuitBreakerStateChanges = meter.CreateCounter<long>(
            "shopsync_circuit_breaker_state_changes_total",
            description: "Circuit breaker durum değişikliği sayısı");
    }
    // Kullanım metotları
    public void OrderCreated() => _ordersCreated.Add(1);
    public void OrderConfirmed() => _ordersConfirmed.Add(1);
    public void OrderCancelled() => _ordersCancelled.Add(1);
    public void OrderExpired() => _ordersExpired.Add(1);
    public void DeadLetterEnqueued() => _deadLetterCount.Add(1);
    public void RecordReservationDuration(double ms) => _reservationDurationMs.Record(ms);
    public void RecordConfirmationDuration(double ms) => _confirmationDurationMs.Record(ms);
    public void RecordGrpcCallDuration(double ms) => _grpcCallDurationMs.Record(ms);

    // Circuit breaker durum değişikliği metodu
    public void CircuitBreakerStateChanged(string newState) =>
        _circuitBreakerStateChanges.Add(1, new KeyValuePair<string, object?>("state", newState));
}
