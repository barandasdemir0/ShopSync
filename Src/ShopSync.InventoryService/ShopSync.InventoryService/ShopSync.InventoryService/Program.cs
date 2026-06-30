using ShopSync.InventoryService.Extension;

var builder = WebApplication.CreateBuilder(args);

#region extension tanımlamaları

builder.AddMonitoring();



#endregion

//grpc service
builder.Services.AddGrpc();

var app = builder.Build();

//
app.UseMonitoring();


// Configure the HTTP request pipeline.

app.MapGet("/", () => "ShopSync InventoryService is running.");

app.Run();
