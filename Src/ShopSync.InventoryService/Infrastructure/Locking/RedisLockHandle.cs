using StackExchange.Redis;

namespace ShopSync.InventoryService.Infrastructure.Locking;

internal sealed class RedisLockHandle : IAsyncDisposable
{
    // Redis Lua betiği, kilidi yalnızca değer eşleştiğinde serbest bırakır.
    //KEYS[1]: Senin kilit anahtarın (Örn: lock:inventory:PHONE-001).
    //ARGV[1]: Senin oluşturduğun o eşsiz Guid değeri(Yani kilidi alan kişinin jetonu).
    //return 0: Eğer değerler eşleşmezse veya kilit çoktan silinmişse 0 döndürür. Eğer başarıyla silerse redis.call('del') komutu otomatik olarak 1 döndürür. Böylece C# tarafında kilidin senin tarafınan silinip silinmediğini kontrol edebilirsin.

    private const string ReleaseLockScript = """
    if redis.call('get', KEYS[1]) == ARGV[1] then
        return redis.call('del', KEYS[1])
    else
        return 0
    end
    """;

    private readonly IDatabase _db;
    private readonly IReadOnlyList<string> _keys;
    private readonly string _lockValue;
    private readonly ILogger _logger;
    private bool _disposed;

    public RedisLockHandle(
        IDatabase db,
        IReadOnlyList<string> keys,
        string lockValue,
        ILogger logger)
    {
        _db = db;
        _keys = keys;
        _lockValue = lockValue;
        _logger = logger;
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
                await db.ScriptEvaluateAsync(
                    ReleaseLockScript, // Lua betiği
                    new RedisKey[] { key }, // Kilit anahtarı
                    new RedisValue[] { lockValue }); // Kilidi serbest bırakmak için kullanılan eşsiz değer (lockValue)

                logger.LogDebug("Kilit serbest bırakıldı: {Key}", key);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Kilit serbest bırakılırken hata oluştu: {Key}",
                    key);
            }
        }
    }
}
