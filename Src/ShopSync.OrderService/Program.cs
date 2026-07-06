using Mapster;
using MapsterMapper;
using Scalar.AspNetCore;
using Serilog;
using ShopSync.OrderService.Exceptions;
using ShopSync.OrderService.Extension;
using System.Reflection;


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("ShopSync OrderService başlatılıyor...");

    


    var builder = WebApplication.CreateBuilder(args);


    builder.AddAppConfigurations();
    builder.AddInfrastructureServices();
    builder.AddGrpcClients();
    builder.AddMonitoring();
    builder.AddHealthCheckServices();
    builder.AddBackgroundJobs();


    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddCustomRateLimiting(builder.Configuration);





    var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
    typeAdapterConfig.Scan(Assembly.GetExecutingAssembly());

    builder.Services.AddSingleton(typeAdapterConfig);

    builder.Services.AddScoped<IMapper, ServiceMapper>();



    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        // Tarih dönüştürücümüzü JSON ayarlarına ekliyoruz
        options.JsonSerializerOptions.Converters.Add(new ShopSync.OrderService.Extension.DateTimeJsonConverter());
    });
    builder.Services.AddOpenApi();


    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }
    app.UseRateLimiter();

    app.UseExceptionHandler();

    app.UseMonitoring();
    app.UseHealthCheckEndpoints();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ShopSync OrderService başlatılırken kritik hata oluştu.");
}
finally
{
    Log.CloseAndFlush();
}
