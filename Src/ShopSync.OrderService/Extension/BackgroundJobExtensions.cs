using ShopSync.OrderService.BackgroundJobs;
using ShopSync.OrderService.Infrastructure.Persistence;

namespace ShopSync.OrderService.Extension;

public static class BackgroundJobExtensions
{
    public static WebApplicationBuilder AddBackgroundJobs(this WebApplicationBuilder builder)
    {
        // MongoDB index oluşturma servisi (başlangıçta bir kez çalışır)
        builder.Services.AddHostedService<MongoDbIndexInitializer>(); 
        builder.Services.AddHostedService<OrderExpirationJob>();

        return builder;
    }
}
//addhostedservicenin amacı , uygulama başlatıldığında belirli bir arka plan işini çalıştırmaktır. Bu, genellikle uzun süreli veya sürekli çalışan görevler için kullanılır. Örneğin, veritabanı indekslerini oluşturmak veya temizleme işlemleri yapmak gibi görevler için uygundur.