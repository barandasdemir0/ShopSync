using ShopSync.InventoryService.Infrastructure.HealthChecks;

namespace ShopSync.InventoryService.Extension;

public static class HealthCheckExtensions
{
    public static WebApplicationBuilder AddHealthCheckServices(
      this WebApplicationBuilder builder)
    {
        // Add health checks for MongoDB and Redis
        builder.Services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>(
                "mongodb",
                tags: new[] { "database", "ready" })
            .AddCheck<RedisHealthCheck>(
                "redis",
                tags: new[] { "cache", "ready" });
        return builder;
    }

    public static WebApplication UseHealthCheckEndpoints(
      this WebApplication app)
    {
        // /healthz → Tüm check'leri çalıştırır (genel sağlık durumu)
        // Docker ve Kubernetes bu endpoint'i kullanır
        app.MapHealthChecks("/healthz");
        return app;
    }
}
