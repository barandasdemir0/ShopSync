using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSync.OrderService.Infrastructure.Persistence;

namespace ShopSync.OrderService.Infrastructure.HealthChecks;

public sealed class MongoDbHealthCheck : IHealthCheck
{
    private readonly MongoDbContext _context;
    public MongoDbHealthCheck(MongoDbContext context)
    {
        _context = context;
    }
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _context.Client.GetDatabase("admin");
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB bağlantısı sağlıklı.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB bağlantı hatası.", ex);
        }
    }
}
