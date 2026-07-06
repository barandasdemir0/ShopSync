using ShopSync.InventoryService.Infrastructure.Locking;
using ShopSync.InventoryService.Infrastructure.Metrics;
using ShopSync.InventoryService.Infrastructure.Persistence;
using ShopSync.InventoryService.Infrastructure.Telemetry;
using ShopSync.InventoryService.Repositories;
using StackExchange.Redis;

namespace ShopSync.InventoryService.Extension;

public static class InfrastructureExtensions
{
    public static WebApplicationBuilder AddInfrastructureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<MongoDbContext>();

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connectionString = builder.Configuration["RedisSettings:ConnectionString"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("RedisSettings:ConnectionString configuration is missing.");
            }

            return ConnectionMultiplexer.Connect(connectionString);
        });

        builder.Services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

        builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
        builder.Services.AddSingleton<InventoryMetrics>();
        builder.Services.AddSingleton<InventoryLockMetrics>();



        return builder;
    }

}
