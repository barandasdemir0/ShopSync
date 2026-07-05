using StackExchange.Redis;

namespace ShopSync.OrderService.Infrastructure.Idempotency;

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisIdempotencyService> _logger;

    private const string KeyPrefix = "idempotency:"; 
    public RedisIdempotencyService(
        IConnectionMultiplexer redis,
        ILogger<RedisIdempotencyService> logger)
    {
        _redis = redis;
        _logger = logger;
    }



    public async Task<string?> GetOrderIdAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var redisKey = KeyPrefix + idempotencyKey; // Redis key'ini oluştur
        var value = await db.StringGetAsync(redisKey); // Redis'ten key'in value'sunu al
        if (!value.HasValue) // Eğer key yoksa null döner
        {
            return null;
        }

        return value.ToString(); // Eğer key varsa value'yu döner
    }

    // Bu method, verilen idempotencyKey için Redis'te bir kayıt oluşturur ve orderId'yi key'e bağlar. Eğer key daha önce kullanılmışsa, mevcut value'yu orderId ile günceller ve TTL'yi korur. TTL , key'in ne kadar süre sonra silineceğini belirler. Bu method, işlemin tamamlandığını ve orderId'nin artık kullanılabilir olduğunu belirtir.
    public async Task SetOrderIdAsync(string idempotencyKey, string orderId, TimeSpan expiry, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var redisKey = KeyPrefix + idempotencyKey;
        // Mevcut key'in value'sunu orderId ile güncelle (TTL'yi koru)
        await db.StringSetAsync(redisKey, orderId, expiry);
    }


    // bu method, verilen idempotencyKey için Redis'te bir kayıt oluşturur ve eğer key daha önce kullanılmamışsa true döner, aksi halde false döner.
    public async Task<bool> TryAcquireAsync(string idempotencyKey, TimeSpan expiry, CancellationToken ct = default)
    {
        // Redis bağlantısını al
        var db = _redis.GetDatabase();
        var redisKey = KeyPrefix + idempotencyKey; // Redis key'ini oluştur
        // SET NX: Atomic olarak key yoksa yaz, varsa yazma
        var acquired = await db.StringSetAsync(
            redisKey,
            "PROCESSING",
            expiry,
            When.NotExists);
        if (!acquired)
        {
            _logger.LogInformation(
                "Idempotent istek tespit edildi. Key: {Key}", idempotencyKey);
        }
        return acquired;
    }
}

//idempotency , aynı işlemin birden fazla kez yapılmasının sistem üzerinde yan etkisi olmamasını ifade eder. Örneğin, bir ödeme işlemi birden fazla kez tetiklenirse, sadece bir kez işleme alınması gerekir