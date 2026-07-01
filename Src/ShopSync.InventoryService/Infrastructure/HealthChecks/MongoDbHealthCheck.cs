using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using ShopSync.InventoryService.Infrastructure.Persistence;

namespace ShopSync.InventoryService.Infrastructure.HealthChecks;

public sealed class MongoDbHealthCheck : IHealthCheck
{

    private readonly MongoDbContext _dbContext;
    public MongoDbHealthCheck(MongoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // MongoDB'ye "ping" komutu gönder
            var database = _dbContext.Client.GetDatabase("admin");
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB bağlantısı sağlıklı.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "MongoDB bağlantısı kurulamadı.",
                exception: ex);
        }
    }
}
