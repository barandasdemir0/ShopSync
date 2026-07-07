using Grpc.Core;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using ShopSync.InventoryService.Protos;
using ShopSync.OrderService.Configuration;
using ShopSync.OrderService.Infrastructure.GrpcClients;
using ShopSync.OrderService.Infrastructure.Telemetry;



namespace ShopSync.OrderService.Extension;

public static class GrpcClientExtensions
{
    public static WebApplicationBuilder AddGrpcClients(this WebApplicationBuilder builder)
    {
        var pollySettings = builder.Configuration
            .GetSection("PollySettings")
            .Get<PollySettings>() ?? new PollySettings();
       
        builder.Services.AddGrpcClient<InventoryGrpc.InventoryGrpcClient>((serviceProvider, options) =>
        {
            var grpcSettings = serviceProvider.GetRequiredService<IOptions<InventoryGrpcSettings>>().Value;
            options.Address = new Uri(grpcSettings.Address);
        });
        // 2. Sistemin Her Yerinden Çağrılabilir "Global" Polly Kalkanı Kuruluyor
        builder.Services.AddResiliencePipeline("inventory-pipeline", (resilienceBuilder, context) =>
        {
            var logger = context.ServiceProvider.GetService<ILogger<InventoryGrpcClient>>();
            var metrics = context.ServiceProvider.GetService<OrderMetrics>();
            resilienceBuilder.AddRetry(new RetryStrategyOptions
            {
                // SADECE RpcException fırlarsa yakala (Çünkü gRPC bağlantı hataları hep RpcException'dır!)
                ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is RpcException),
                MaxRetryAttempts = pollySettings.RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(pollySettings.RetryBaseDelayMs),
                OnRetry = args =>
                {
                    var rpcEx = args.Outcome.Exception as RpcException;
                    logger?.LogWarning("gRPC Retry #{Attempt}. Bekleme: {Delay}ms. Sebep: {StatusCode}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        rpcEx?.StatusCode);
                    return ValueTask.CompletedTask;
                }
            });
            resilienceBuilder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is RpcException),
                FailureRatio = 0.5,
                MinimumThroughput = pollySettings.CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(pollySettings.CircuitBreakerDurationSeconds),
                OnOpened = args =>
                {
                    logger?.LogError("CIRCUIT BREAKER AÇILDI! InventoryService'e istek gönderimi {Duration}s durduruldu.", pollySettings.CircuitBreakerDurationSeconds);
                    metrics?.CircuitBreakerStateChanged("Open");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger?.LogInformation("Circuit Breaker kapandı. InventoryService iletişimi normale döndü.");
                    metrics?.CircuitBreakerStateChanged("Closed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger?.LogInformation("Circuit Breaker yarı açık. Deneme isteği gönderiliyor...");
                    metrics?.CircuitBreakerStateChanged("HalfOpen");
                    return ValueTask.CompletedTask;
                }
            });
        });
        builder.Services.AddScoped<IInventoryGrpcClient, InventoryGrpcClient>();
        return builder;
    }   
}
