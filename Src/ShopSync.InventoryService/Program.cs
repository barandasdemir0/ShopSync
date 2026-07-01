using Serilog;
using ShopSync.InventoryService.Extension;
using ShopSync.InventoryService.Services;


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("ShopSync InventoryService başlatılıyor...");

    var builder = WebApplication.CreateBuilder(args);

    #region extension tanımlamaları

  

    builder.AddAppConfigurations();
    builder.AddGrpcServices();
    builder.AddInfrastructureServices();
    builder.AddMonitoring();
    builder.AddHealthCheckServices();   
    builder.AddBackgroundJobs();       


    #endregion



    var app = builder.Build();

    
    app.UseMonitoring();
    app.UseHealthCheckEndpoints();


    app.MapGrpcService<InventoryGrpcService>();

    // Configure the HTTP request pipeline.

    app.MapGet("/", () => "ShopSync InventoryService is running.");

    app.Run();
}
catch (Exception ex)
{

    Log.Fatal(ex, "ShopSync InventoryService başlatılırken kritik hata oluştu.");
}

finally
{
    Log.CloseAndFlush();
}


