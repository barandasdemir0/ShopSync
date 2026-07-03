using ShopSync.OrderService.Exceptions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();


var app = builder.Build();

app.UseExceptionHandler();

app.MapControllers();


app.MapGet("/", () => "Hello World!");



app.Run();
