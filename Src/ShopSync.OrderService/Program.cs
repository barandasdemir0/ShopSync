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




    var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
    typeAdapterConfig.Scan(Assembly.GetExecutingAssembly());

    builder.Services.AddSingleton(typeAdapterConfig);

    builder.Services.AddScoped<IMapper, ServiceMapper>();



    builder.Services.AddControllers();
    builder.Services.AddOpenApi();


    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseExceptionHandler();

    app.UseMonitoring();
    app.UseHealthCheckEndpoints();
    app.MapControllers();


    app.MapGet("/", () => "Hello World!");



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
