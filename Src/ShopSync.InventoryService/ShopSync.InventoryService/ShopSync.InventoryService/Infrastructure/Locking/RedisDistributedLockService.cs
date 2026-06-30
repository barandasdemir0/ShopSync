using StackExchange.Redis;

namespace ShopSync.InventoryService.Infrastructure.Locking;

public sealed class RedisDistributedLockService : IDistributedLockService
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<RedisDistributedLockService> _logger;

    public RedisDistributedLockService(IConnectionMultiplexer redis, ILogger<RedisDistributedLockService> logger)
    {
        _redisDb = redis.GetDatabase();
        _logger = logger;
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


        // Anahtarlar üzerinde sırayla kilit almaya çalışılır.
        try
        {
            // Anahtarlar sıralı bir şekilde işlenir, bu sayede deadlock riski azaltılır.
            foreach (var key in sortedKeys)
            {
                // Kilit alınamadığında tekrar denemek için bir değişken tanımlanır.
                var acquired = false;

                // Maksimum deneme sayısı kadar kilit almaya çalışılır.
                for (int retry = 0; retry < MaxRetries; retry++)
                {
                    // CancellationToken kontrolü yapılır, eğer iptal edilmişse OperationCanceledException fırlatılır.
                    cancellationToken.ThrowIfCancellationRequested();

                    // Redis üzerinde SETNX komutu ile kilit almaya çalışılır. Eğer kilit alınabilirse true döner, aksi halde false döner.
                    acquired = await _redisDb.StringSetAsync(
                        key,// Kilit değeri olarak benzersiz bir GUID kullanılır.
                        lockValue, //lockValue,
                        lockExpiry, // Kilit süresi
                        When.NotExists); // Sadece kilit mevcut değilse (NotExists) kilit alınır.


                    // Eğer kilit alınabilmişse acquiredLocks listesine eklenir ve döngüden çıkılır.
                    if (acquired)
                    {
                        _logger.LogDebug("Kilit alındı: {Key}", key);
                        acquiredLocks.Add(key);
                        break;
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
                _logger);
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
