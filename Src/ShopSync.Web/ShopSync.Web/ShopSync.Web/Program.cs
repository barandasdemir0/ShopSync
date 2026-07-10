using Polly;
using Refit;
using ShopSync.Web.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// JSON ayarlarımızı oluşturuyoruz
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

jsonOptions.Converters.Add(new LenientDateTimeConverter());

// Refit'i bu özel JSON ayarlarıyla projeye dahil ediyoruz

// API adresini appsettings.json'dan okuyoruz
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]!;

// Refit
// Refit ve Polly Entegrasyonu
builder.Services.AddRefitClient<IShopSyncApi>(new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(jsonOptions)
})
.ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(500)));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
