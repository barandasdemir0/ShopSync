using ShopSync.InventoryService.Infrastructure.Telemetry;
using StackExchange.Redis;
using System.Diagnostics;

namespace ShopSync.InventoryService.Infrastructure.Locking;

internal sealed class RedisLockHandle : IAsyncDisposable
{


    private readonly IDatabase _db;
    private readonly IReadOnlyList<string> _keys;
    private readonly string _lockValue;
    private readonly ILogger _logger;

    private readonly InventoryLockMetrics _metrics;
    private readonly Stopwatch _holdStopwatch;
    private bool _disposed;




    public RedisLockHandle(
        IDatabase db,
        IReadOnlyList<string> keys,
        string lockValue,
        ILogger logger,
        InventoryLockMetrics metrics,
        Stopwatch holdStopwatch)
    {
        _db = db;
        _keys = keys;
        _lockValue = lockValue;
        _logger = logger;
        _metrics = metrics;
        _holdStopwatch = holdStopwatch;
    }


    // DisposeAsync metodu, kilidi serbest bırakmak için çağrılır.
    public async ValueTask DisposeAsync()
    {
        // Eğer zaten dispose edilmişse, tekrar serbest bırakma işlemi yapma.
        if (_disposed)
        {
            return;
        }

        // Dispose işlemi başlatılıyor, kilidi serbest bırak.
        _disposed = true;

        _holdStopwatch.Stop();
        var elapsedMs = _holdStopwatch.ElapsedMilliseconds;

        foreach (var key in _keys)
        {
            // "lock:inventory:SKU" formatından sadece SKU'yu ayıklıyoruz
            var sku = key.Replace("lock:inventory:", "", StringComparison.OrdinalIgnoreCase); //ordinal ignore case ile büyük küçük harf farketmez ve "lock:inventory:", "", burası boş string ile değiştiriliyor ve sku elde ediliyor
            _metrics.RecordLockHold(elapsedMs, sku);
        }

        // Kilidi serbest bırakmak için Redis Lua betiğini çalıştır.
        await ReleaseAsync(_db, _keys, _lockValue, _logger);

        // Dispose işlemi tamamlandı, logla.
        _logger.LogDebug(
            "Redis lock handle dispose edildi. Serbest bırakılan kilit sayısı: {Count}",
            _keys.Count);
    }


    // Kilidi serbest bırakmak için kullanılan yardımcı metot.
    public static async Task ReleaseAsync(
        IDatabase db,
        IEnumerable<string> keys,
        string lockValue,
        ILogger logger)
    {
        // Her bir kilit anahtarı için Lua betiğini çalıştırarak kilidi serbest bırak.
        foreach (var key in keys)
        {
            try
            {
                // StackExchange.Redis'in yerleşik atomik kilit serbest bırakma metodu
                // Arka planda aynı Lua scriptini çalıştırır ama sen Lua görmezsin!
                var released = await db.LockReleaseAsync(key, lockValue);
                if (released)
                {
                    logger.LogDebug("Kilit serbest bırakıldı: {Key}", key);
                }
                else
                {
                    logger.LogWarning("Kilit serbest bırakılamadı (başka biri almış olabilir): {Key}", key);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Kilit serbest bırakılırken hata oluştu: {Key}", key);
            }
        }
    }
}
