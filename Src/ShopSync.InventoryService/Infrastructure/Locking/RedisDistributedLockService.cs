using ShopSync.InventoryService.Infrastructure.Telemetry;
using StackExchange.Redis;
using System.Diagnostics;

namespace ShopSync.InventoryService.Infrastructure.Locking;

public sealed class RedisDistributedLockService : IDistributedLockService
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<RedisDistributedLockService> _logger;
    private readonly InventoryLockMetrics _lockMetrics;

    public RedisDistributedLockService(IConnectionMultiplexer redis, ILogger<RedisDistributedLockService> logger, InventoryLockMetrics lockMetrics)
    {
        _redisDb = redis.GetDatabase();
        _logger = logger;
        _lockMetrics = lockMetrics;
    }

    // Kilidi almaya çalışırken her denemeler arası bekleme süresi
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    // Maksimum deneme sayısı 100 deneme × 100ms = 10 saniye boyunca dener, sonra hata fırlatır.
    private const int MaxRetries = 100;


    // AcquireLocksAsync metodu, verilen anahtarlar için kilitleri almaya çalışır.
    public async Task<IAsyncDisposable> AcquireLocksAsync(IEnumerable<string> keys, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {



        // Anahtarlar null ise ArgumentNullException fırlatılır.
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }


        // Kilit süresi belirtilmemişse varsayılan olarak 30 saniye kullanılır.
        var lockExpiry = expiry ?? TimeSpan.FromSeconds(30);


        // Anahtarlar temizlenir, boş veya null olanlar filtrelenir, büyük harfe çevrilir ve alfabetik sıralanır.
        var sortedKeys = keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => $"lock:inventory:{k.Trim().ToUpperInvariant()}")
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        if (sortedKeys.Count == 0) // Eğer kilitlenecek anahtar yoksa ArgumentException fırlatılır.
        {
            throw new ArgumentException("Kilitlenecek en az bir key verilmelidir.", nameof(keys));
        }

        // Kilit değeri olarak benzersiz bir GUID oluşturulur.
        var lockValue = Guid.NewGuid().ToString("N");


        // Kilit alınan anahtarları tutmak için bir liste oluşturulur.
        var acquiredLocks = new List<string>();

        // 6. KİLİT SAHİPLİK KRONOMETRESİNİ BAŞLAT
        var holdStopwatch = Stopwatch.StartNew();



        // Anahtarlar üzerinde sırayla kilit almaya çalışılır.
        try
        {
            // Anahtarlar sıralı bir şekilde işlenir, bu sayede deadlock riski azaltılır.
            foreach (var key in sortedKeys)
            {

                var sku = key.Replace("lock:inventory:", "", StringComparison.OrdinalIgnoreCase);

                // Kilit alınamadığında tekrar denemek için bir değişken tanımlanır.
                var acquired = false;

                // 7. KİLİT BEKLEME SÜRESİ KRONOMETRESİNİ BAŞLAT
                var lockWaitStopwatch = Stopwatch.StartNew();

                // Maksimum deneme sayısı kadar kilit almaya çalışılır.
                for (int retry = 0; retry < MaxRetries; retry++)
                {
                    // CancellationToken kontrolü yapılır, eğer iptal edilmişse OperationCanceledException fırlatılır.
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        acquired = await _redisDb.LockTakeAsync(key, lockValue, lockExpiry);


                    }
                    catch (Exception)
                    {
                        // 8. HATA DURUMUNDA CONTENTION METRİĞİNİ YAZ
                        _lockMetrics.LockContention(sku);
                        throw;
                    }

                   


                    // Eğer kilit alınabilmişse acquiredLocks listesine eklenir ve döngüden çıkılır.
                    if (acquired)
                    {
                        _logger.LogDebug("Kilit alındı: {Key}", key);
                        acquiredLocks.Add(key);

                        // 9. KİLİT ALINDI METRİKLERİNİ KAYDET
                        lockWaitStopwatch.Stop();
                        _lockMetrics.RecordLockWait(lockWaitStopwatch.ElapsedMilliseconds, sku);
                        _lockMetrics.LockAcquired(sku);
                        break;
                    }

                    // 10. TÜM DENEMELERE RAĞMEN ALINAMADIYSA TIMEOUT YAZ
                    if (retry == MaxRetries - 1)
                    {
                        lockWaitStopwatch.Stop();
                        _lockMetrics.LockFailed(sku);
                        _lockMetrics.LockTimeout(sku);
                    }


                    _logger.LogDebug(
                        "Kilit meşgul, tekrar deneniyor: {Key} (Deneme: {Retry}/{Max})",
                        key,
                        retry + 1,
                        MaxRetries);

                    await Task.Delay(RetryDelay, cancellationToken);
                }

                if (!acquired)
                {
                    _logger.LogWarning(
                        "Kilit alınamadı, zaman aşımı: {Key}. Tüm kilitler geri bırakılıyor.",
                        key);

                    throw new TimeoutException(
                        $"'{key}' için distributed lock alınamadı. {MaxRetries} deneme sonrası zaman aşımı.");
                }
            }

            _logger.LogInformation(
                "Tüm kilitler başarıyla alındı. Kilit sayısı: {Count}",
                acquiredLocks.Count);

            return new RedisLockHandle(
             _redisDb,
             acquiredLocks,
             lockValue,
             _logger,
             _lockMetrics,
             holdStopwatch);
        }
        catch
        {
            await RedisLockHandle.ReleaseAsync(
                _redisDb,
                acquiredLocks,
                lockValue,
                _logger);

            throw;
        }
    }


}
