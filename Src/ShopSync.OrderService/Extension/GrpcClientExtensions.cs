using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using ShopSync.InventoryService.Protos;
using ShopSync.OrderService.Configuration;
using ShopSync.OrderService.Infrastructure.GrpcClients;



namespace ShopSync.OrderService.Extension;

public static class GrpcClientExtensions
{
    public static WebApplicationBuilder AddGrpcClients(this WebApplicationBuilder builder)
    {
        // Polly ayarlarını oku
        var pollySettings = builder.Configuration
            .GetSection("PollySettings")
            .Get<PollySettings>() ?? new PollySettings();

        builder.Services.AddGrpcClient<InventoryGrpc.InventoryGrpcClient>((serviceProvider, options) =>
        {
            // InventoryService'in gRPC adresi (Örn: "http://localhost:5001")
            var grpcSettings = serviceProvider.GetRequiredService<IOptions<InventoryGrpcSettings>>().Value;
            options.Address = new Uri(grpcSettings.Address);
        }).AddResilienceHandler("inventory-resilience", (resilienceBuilder, handlerContext) =>
        {

            var logger = handlerContext.ServiceProvider.GetService<ILogger<InventoryGrpc.InventoryGrpcClient>>();

            resilienceBuilder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = pollySettings.RetryCount,
                BackoffType = DelayBackoffType.Exponential, // 1 2 4 diye arttırır
                Delay = TimeSpan.FromMilliseconds(pollySettings.RetryBaseDelayMs), // Başlangıç gecikmesi
                OnRetry = args =>
                {
                   
                    logger?.LogWarning(
                        "gRPC Retry #{Attempt}. Bekleme: {Delay}ms. Sebep: {Outcome}",
                        args.AttemptNumber, // Retry sayısı
                        args.RetryDelay.TotalMilliseconds, // Gecikme süresi
                        args.Outcome.Result?.StatusCode); // Hata kodu
                    return ValueTask.CompletedTask; // ValueTask.CompletedTask, çünkü OnRetry async bir method değil
                }

            });

            resilienceBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5, // %50 başarısızlık durumunda devre kesici açılır  10 istekten 5'i başarısız olursa devre kesici açılır
                MinimumThroughput = pollySettings.CircuitBreakerFailureThreshold, //circuit breaker'ın açılabilmesi için minimum istek sayısı
                SamplingDuration = TimeSpan.FromSeconds(30), // Örnekleme süresi (30 saniye boyunca gelen istekler değerlendirilir)
                BreakDuration = TimeSpan.FromSeconds(pollySettings.CircuitBreakerDurationSeconds), // Devre kesici açıldığında ne kadar süreyle istekleri engelleyeceği
                OnOpened = args =>
                {
                   
                    logger?.LogError(
                        "CIRCUIT BREAKER AÇILDI! InventoryService'e istek gönderimi {Duration}s durduruldu.",
                        pollySettings.CircuitBreakerDurationSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    
                    logger?.LogInformation("Circuit Breaker kapandı. InventoryService iletişimi normale döndü.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    
                    logger?.LogInformation("Circuit Breaker yarı açık. Deneme isteği gönderiliyor...");
                    return ValueTask.CompletedTask;
                }
            });
       
    });
        builder.Services.AddScoped<IInventoryGrpcClient, InventoryGrpcClient>();
        return builder;
    }   
}
