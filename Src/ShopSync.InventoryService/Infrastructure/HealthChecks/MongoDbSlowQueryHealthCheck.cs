using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using ShopSync.InventoryService.Infrastructure.Persistence;
using System.Diagnostics.Metrics;

namespace ShopSync.InventoryService.Infrastructure.HealthChecks;

public sealed class MongoDbSlowQueryHealthCheck:IHealthCheck
{
    private readonly MongoDbContext _context;
    private readonly Counter<long> _slowQueryCounter;
    public MongoDbSlowQueryHealthCheck(MongoDbContext context, IMeterFactory meterFactory)
    {
        _context = context;
        var meter = meterFactory.Create("ShopSync.InventoryService");
        _slowQueryCounter = meter.CreateCounter<long>(
            "shopsync_mongodb_slow_queries_total",
            description: "MongoDB yavaş sorgu (>500ms) sayısı");
    }
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // bu kontrol, MongoDB'nin profil verilerini kullanarak son 5 dakikada 500ms'den uzun süren sorguları sayar.
            var db = _context.Client.GetDatabase("shopSync_inventory");
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


            // eğer yavaş sorgu sayısı 0'dan büyükse, counter'ı artır
            if (slowQueryCount > 0)
            {
                _slowQueryCounter.Add(slowQueryCount);
            }


            // eğer yavaş sorgu sayısı 10'dan fazla ise, sağlık durumu bozuk olarak işaretle
            if (slowQueryCount > 10)
            {
                //degraded, sistemin çalıştığını ama performans sorunları yaşadığını gösterir. Bu durumda, yavaş sorguların sayısı 10'dan fazla olduğunda, sağlık durumu bozuk olarak işaretlenir.
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
