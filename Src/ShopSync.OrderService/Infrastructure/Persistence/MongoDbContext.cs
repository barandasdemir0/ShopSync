using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ShopSync.OrderService.Configuration;
using ShopSync.OrderService.Models;

namespace ShopSync.OrderService.Infrastructure.Persistence;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly MongoClient _client;
    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        _client = new MongoClient(settings.Value.ConnectionString);
        _database = _client.GetDatabase(settings.Value.DatabaseName);
    }


    // Sipariş koleksiyonu
    public IMongoCollection<Order> Orders
        => _database.GetCollection<Order>("orders");


    // MongoDB Client'ına erişim (transaction başlatmak için)
    public IMongoClient Client => _client;
}
