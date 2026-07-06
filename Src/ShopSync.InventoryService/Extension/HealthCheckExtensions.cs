using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ShopSync.InventoryService.Infrastructure.HealthChecks;
using System.Text.Json;

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
                tags: new[] 
                { 
                    "database", "ready" 
                })
            .AddCheck<RedisHealthCheck>(
                "redis",
                tags: new[] 
                { 
                    "cache", "ready" 
                })
            .AddCheck<MongoDbSlowQueryHealthCheck>("mongodb_slow_queries", tags: new[]
            { 
                "database" 
            });

        return builder;
    }

    public static WebApplication UseHealthCheckEndpoints(this WebApplication app)
    {
        // /healthz → Tüm check'leri çalıştırır ve detaylı JSON çıktısı döner
        app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            // Her bir bağımlılığın (MongoDB, Redis) durumunu ayrı ayrı gösteren
            // zengin JSON formatında çıktı üret.
            ResponseWriter = async (context, report) =>
            {
                // JSON çıktısını oluşturmak için System.Text.Json kullanıyoruz.
                context.Response.ContentType = "application/json";
                var result = new
                {
                    // Overall health status
                    status = report.Status.ToString(),
                    totalDuration = report.TotalDuration.TotalMilliseconds + "ms",
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key, // MongoDB, Redis
                        status = e.Value.Status.ToString(), // Healthy, Unhealthy, Degraded
                        description = e.Value.Description, // Check'in açıklaması
                        duration = e.Value.Duration.TotalMilliseconds + "ms", // Check'in çalıştırılma süresi
                        exception = e.Value.Exception?.Message // Check sırasında oluşan hata mesajı (varsa)
                    })
                };
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(result, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
            }
        });
        return app;
    }
}
