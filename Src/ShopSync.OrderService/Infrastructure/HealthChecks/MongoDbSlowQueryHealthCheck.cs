using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using ShopSync.OrderService.Infrastructure.Persistence;

namespace ShopSync.OrderService.Infrastructure.HealthChecks;

public sealed class MongoDbSlowQueryHealthCheck:IHealthCheck
{
    private readonly MongoDbContext _context;
    public MongoDbSlowQueryHealthCheck(MongoDbContext context)
    {
        _context = context;
    }
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // bu kontrol, MongoDB'nin profil verilerini kullanarak son 5 dakikada 500ms'den uzun süren sorguları sayar.
            var db = _context.Client.GetDatabase("shopSync_order");
            // alt sorgu: system.profile koleksiyonunu kullanarak yavaş sorguları sayar
            var profileCollection = db.GetCollection<BsonDocument>("system.profile");

            // son 5 dakikada 500ms'den uzun süren sorguları filtrele
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
            // filtre: millis > 500 ve ts > fiveMinutesAgo
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Gt("millis", 500),
                Builders<BsonDocument>.Filter.Gt("ts", fiveMinutesAgo));
            // yavaş sorgu sayısını al
            var slowQueryCount = await profileCollection.CountDocumentsAsync(
                filter, cancellationToken: cancellationToken);
            // eğer yavaş sorgu sayısı 10'dan fazla ise, sağlık durumu bozuk olarak işaretle
            if (slowQueryCount > 10)
            {
                return HealthCheckResult.Degraded(
                   $"Son 5 dakikada {slowQueryCount} yavaş sorgu tespit edildi (>500ms).");
            }
               
            return HealthCheckResult.Healthy(
                $"MongoDB performansı normal. Yavaş sorgu sayısı: {slowQueryCount}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB profiler kontrol hatası.", ex);
        }
    }
}
