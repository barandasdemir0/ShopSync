using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ShopSync.OrderService.Infrastructure.HealthChecks;
using System.Text.Json;

namespace ShopSync.OrderService.Extension;

public static class HealthCheckExtensions
{
    public static WebApplicationBuilder AddHealthCheckServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongodb", tags: new[] { "database", "ready" })
            .AddCheck<RedisHealthCheck>("redis", tags: new[] { "cache", "ready" });
        return builder;
    }
    public static WebApplication UseHealthCheckEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var result = new
                {
                    status = report.Status.ToString(), // Genel sağlık durumu (Sağlıklı, Kötüleşmiş, Sağlıksız)
                    totalDuration = report.TotalDuration.TotalMilliseconds + "ms", // Toplam sağlık kontrolü süresi
                    checks = report.Entries.Select(e => new // Her bir sağlık kontrolü için detaylar
                    {
                        name = e.Key, // Sağlık kontrolünün adı redis, mongodb gibi
                        status = e.Value.Status.ToString(), // Sağlık kontrolünün durumu (Sağlıklı, Kötüleşmiş, Sağlıksız)
                        description = e.Value.Description, // Sağlık kontrolü açıklaması
                        duration = e.Value.Duration.TotalMilliseconds + "ms", // Sağlık kontrolü süresi
                        exception = e.Value.Exception?.Message // Sağlık kontrolü sırasında oluşan hata mesajı (varsa)
                    })
                };
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(result, new JsonSerializerOptions
                    {
                        WriteIndented = true // JSON çıktısını okunabilir hale getirmek için girintili yazdırma
                    }));
            }
        });
        return app;
    }
}
