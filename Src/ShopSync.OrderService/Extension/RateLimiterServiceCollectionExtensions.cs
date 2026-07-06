using ShopSync.OrderService.Configuration;
using System.Threading.RateLimiting;

namespace ShopSync.OrderService.Extension;

public static class RateLimiterServiceCollectionExtensions
{
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Ayarları appsettings'den oku
        var rateLimitingSettings = new RateLimitingSettings();
        configuration.GetSection("RateLimiting").Bind(rateLimitingSettings);

        // 2. Rate Limiter'ı sisteme ekle
        services.AddRateLimiter(options =>
        {
            // bütün endpointler için global bir rate limiter tanımla
            //partitioning yapısı ile her endpoint için ayrı limitler tanımlanabilir
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {

                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ipAddress,
                    factory: partitionKey =>
                    {
                        return new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = rateLimitingSettings.PermitLimit,
                            Window = TimeSpan.FromSeconds(rateLimitingSettings.WindowSeconds),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst, // Kuyrukta bekleyen isteklerin işlenme sırası 
                            QueueLimit = rateLimitingSettings.QueueLimit
                        };
                    });
            });
            // 3. Limit aşıldığında istemciye dönecek standart JSON yanıtı
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                var response = new
                {
                    success = false,
                    code = "TOO_MANY_REQUESTS",
                    message = "Çok fazla istek gönderdiniz. Lütfen daha sonra tekrar deneyin."
                };
                await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
            };
        });
        return services;
    }
}
