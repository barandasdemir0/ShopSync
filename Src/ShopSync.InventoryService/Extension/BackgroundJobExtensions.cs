using ShopSync.InventoryService.BackgroundJobs;
using ShopSync.InventoryService.Configuration;

namespace ShopSync.InventoryService.Extension;

public static class BackgroundJobExtensions
{
    public static WebApplicationBuilder AddBackgroundJobs(
       this WebApplicationBuilder builder)
    {
        //buranın amacı appsettings.json dosyasındaki ExpirationJobSettings bölümünü ExpirationJobSettings sınıfına bağlamaktır. Bu sayede ExpirationJobSettings sınıfı, appsettings.json dosyasındaki ExpirationJobSettings bölümündeki ayarları alabilir ve kullanabilir.
        builder.Services.Configure<ExpirationJobSettings>(
            builder.Configuration.GetSection("ExpirationJobSettings"));

        builder.Services.Configure<ReconciliationJobSettings>(
            builder.Configuration.GetSection("ReconciliationJobSettings"));

        builder.Services.Configure<LowStockAlertSettings>(
            builder.Configuration.GetSection("LowStockAlertSettings"));

        builder.Services.AddHostedService<ReservationExpirationJob>();
        builder.Services.AddHostedService<StockReconciliationJob>();
        builder.Services.AddHostedService<LowStockAlertJob>();
        return builder;
    }
}
