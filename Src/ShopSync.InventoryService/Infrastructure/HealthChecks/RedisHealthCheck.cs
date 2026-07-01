using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ShopSync.InventoryService.Infrastructure.HealthChecks;

public sealed class RedisHealthCheck : IHealthCheck
{

    private readonly IConnectionMultiplexer _redis;
    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Redis'e PING gönder ve PONG yanıtını bekle
            var database = _redis.GetDatabase();
            var pingResult = await database.PingAsync();
            // Ping süresi 1 saniyeden uzunsa "sağlıksız" değil ama "bozulmaya başlamış" say
            if (pingResult.TotalMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Redis yanıt veriyor ama yavaş. Ping süresi: {pingResult.TotalMilliseconds}ms");
            }
            return HealthCheckResult.Healthy(
                $"Redis bağlantısı sağlıklı. Ping: {pingResult.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Redis bağlantısı kurulamadı.",
                exception: ex);
        }
    }
}
