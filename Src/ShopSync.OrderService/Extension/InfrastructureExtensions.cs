using ShopSync.OrderService.Infrastructure.Persistence;
using ShopSync.OrderService.Repositories;
using StackExchange.Redis;

namespace ShopSync.OrderService.Extension;

public static class InfrastructureExtensions
{
    public static WebApplicationBuilder AddInfrastructureServices(this WebApplicationBuilder builder)
    {
        // MongoDB bağlam nesnesi 
        builder.Services.AddSingleton<MongoDbContext>();

        // Redis bağlantısı  ıconnectionmultiplexer'n amacı redis ile etkileşim kurmak ve veri alışverişi yapmaktır.
        builder.Services.AddSingleton<IConnectionMultiplexer>(serviceProvider => // _ işareti, bu durumda kullanılmayan hizmet sağlayıcıyı temsil eder.
        {
            var connectionString = builder.Configuration["RedisSettings:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("RedisSettings:ConnectionString configuration is missing.");
            }
               
            return ConnectionMultiplexer.Connect(connectionString);
        });
        // Sipariş repository
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        return builder;
    }
}
