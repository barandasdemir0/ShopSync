using ShopSync.InventoryService.Extension;
using ShopSync.InventoryService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

#region extension tanımlamaları
// AddMonitoring extension methodunu kullanarak uygulamaya monitoring özelliklerini ekliyoruz.
builder.AddMonitoring();
//addgrpc exception interceptor ile gRPC servislerinde oluşabilecek hataları merkezi bir şekilde yakalayıp yönetiyoruz.
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<GrpcExceptionInterceptor>();
});


#endregion



var app = builder.Build();

//
app.UseMonitoring();


// Configure the HTTP request pipeline.

app.MapGet("/", () => "ShopSync InventoryService is running.");

app.Run();
