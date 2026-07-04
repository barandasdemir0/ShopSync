using ShopSync.OrderService.Infrastructure.Persistence;

namespace ShopSync.OrderService.Extension;

public static class BackgroundJobExtensions
{
    public static WebApplicationBuilder AddBackgroundJobs(this WebApplicationBuilder builder)
    {
        // MongoDB index oluşturma servisi (başlangıçta bir kez çalışır)
        builder.Services.AddHostedService<MongoDbIndexInitializer>();
        return builder;
    }
}
