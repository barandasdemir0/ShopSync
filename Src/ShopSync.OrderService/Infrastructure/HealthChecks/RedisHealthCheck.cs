using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ShopSync.OrderService.Infrastructure.HealthChecks;

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
            var db = _redis.GetDatabase();
            var latency = await db.PingAsync();
            if (latency.TotalMilliseconds > 1000)
                return HealthCheckResult.Degraded($"Redis yavaş yanıt veriyor. Ping: {latency.TotalMilliseconds}ms");
            return HealthCheckResult.Healthy($"Redis bağlantısı sağlıklı. Ping: {latency.TotalMilliseconds:F0}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis bağlantı hatası.", ex);
        }
    }
}
