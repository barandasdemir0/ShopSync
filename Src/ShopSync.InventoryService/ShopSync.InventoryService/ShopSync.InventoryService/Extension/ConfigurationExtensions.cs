using ShopSync.InventoryService.Configuration;

namespace ShopSync.InventoryService.Extension;

public static class ConfigurationExtensions
{
    public static WebApplicationBuilder AddAppConfigurations(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<MongoDbSettings>(
            builder.Configuration.GetSection("MongoDbSettings"));

        builder.Services.Configure<RedisSettings>(
            builder.Configuration.GetSection("RedisSettings"));

        return builder;
    }
}
