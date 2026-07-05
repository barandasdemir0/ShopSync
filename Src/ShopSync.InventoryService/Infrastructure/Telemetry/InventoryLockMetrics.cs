using System.Diagnostics.Metrics;

namespace ShopSync.InventoryService.Infrastructure.Telemetry;

public sealed class InventoryLockMetrics
{
    // Kilit bekleme süresi (acquire etmeye çalışırken geçen süre)
    private readonly Histogram<double> _lockWaitDurationMs;
    // Kilit sahiplik süresi (lock alındıktan dispose edilene kadar)
    private readonly Histogram<double> _lockHoldDurationMs;
    // Kilit alma girişimi sayacı (başarılı/başarısız etiketli)
    private readonly Counter<long> _lockAcquireAttempts;
    // Deadlock tespiti sayacı (timeout ile kilit alınamadığında)
    private readonly Counter<long> _lockTimeouts;
    // Lock contention sayacı (kilit zaten başkası tarafından tutuluyorsa)
    private readonly Counter<long> _lockContentions;






    public InventoryLockMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("ShopSync.InventoryService");
        _lockWaitDurationMs = meter.CreateHistogram<double>(
            "shopsync_lock_wait_duration_ms",
            unit: "ms",
            description: "Kilit almak için beklenen süre (milisaniye)");

        _lockHoldDurationMs = meter.CreateHistogram<double>(
            "shopsync_lock_hold_duration_ms",
            unit: "ms",
            description: "Kilidin tutulma süresi (milisaniye)");

        _lockAcquireAttempts = meter.CreateCounter<long>(
            "shopsync_lock_acquire_attempts_total",
            description: "Kilit alma girişimi sayısı");

        _lockTimeouts = meter.CreateCounter<long>(
            "shopsync_lock_timeouts_total",
            description: "Kilit alma zaman aşımı sayısı (potansiyel deadlock)");

        _lockContentions = meter.CreateCounter<long>(
            "shopsync_lock_contentions_total",
            description: "Kilit çakışması sayısı (başka thread tarafından tutulan)");

    

    }


    // Metotlar, kilit ile ilgili metrikleri kaydetmek için kullanılacak
    public void RecordLockWait(double ms, string sku)
        => _lockWaitDurationMs.Record(ms, new KeyValuePair<string, object?>("sku", sku));

    // Kilit sahiplik süresini kaydetmek için kullanılacak
    public void RecordLockHold(double ms, string sku)
        => _lockHoldDurationMs.Record(ms, new KeyValuePair<string, object?>("sku", sku));

    // Kilit alma girişimi sayısını kaydetmek için kullanılacak
    public void LockAcquired(string sku)
        => _lockAcquireAttempts.Add(1,
            new KeyValuePair<string, object?>("result", "acquired"),
            new KeyValuePair<string, object?>("sku", sku));

    // Kilit alma girişimi başarısız olduğunda kaydedilecek
    public void LockFailed(string sku)
        => _lockAcquireAttempts.Add(1,
            new KeyValuePair<string, object?>("result", "failed"),
            new KeyValuePair<string, object?>("sku", sku));

    // Kilit zaman aşımı (timeout) sayısını kaydetmek için kullanılacak
    public void LockTimeout(string sku)
        => _lockTimeouts.Add(1, new KeyValuePair<string, object?>("sku", sku));

    // Kilit çakışması (contention) sayısını kaydetmek için kullanılacak
    public void LockContention(string sku)
        => _lockContentions.Add(1, new KeyValuePair<string, object?>("sku", sku));


}
