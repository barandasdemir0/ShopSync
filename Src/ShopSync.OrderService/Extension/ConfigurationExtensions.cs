using ShopSync.OrderService.Configuration;

namespace ShopSync.OrderService.Extension;

public static class ConfigurationExtensions
{
    public static WebApplicationBuilder AddAppConfigurations(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<MongoDbSettings>(
            builder.Configuration.GetSection("MongoDbSettings"));

        builder.Services.Configure<RedisSettings>(
            builder.Configuration.GetSection("RedisSettings"));

        builder.Services.Configure<InventoryGrpcSettings>(
            builder.Configuration.GetSection("InventoryGrpcSettings"));

        builder.Services.Configure<PollySettings>(
            builder.Configuration.GetSection("PollySettings"));
        return builder;
    }
}
