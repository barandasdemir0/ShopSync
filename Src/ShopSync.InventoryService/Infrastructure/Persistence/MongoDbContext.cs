using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ShopSync.InventoryService.Configuration;
using ShopSync.InventoryService.Models;

namespace ShopSync.InventoryService.Infrastructure.Persistence;

public sealed class MongoDbContext
{

    private readonly IMongoDatabase _database;
    private readonly MongoClient _client;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {

        // tüm uygulama boyunca tek bir instance yeterlidir.
        _client = new MongoClient(settings.Value.ConnectionString);
        // Belirtilen veritabanına bağlan.
        _database = _client.GetDatabase(settings.Value.DatabaseName);

    }

    //Stok bilgilerinin tutulduğu collection.
    public IMongoCollection<InventoryItem> InventoryItems
       => _database.GetCollection<InventoryItem>("inventory_items");


    // Tüm stok değişikliklerinin loglandığı collection.
    public IMongoCollection<InventoryTransactionLog> TransactionLogs
      => _database.GetCollection<InventoryTransactionLog>("transaction_logs");

    // Expiration job'ının checkpoint bilgilerinin tutulduğu collection.
    public IMongoCollection<ExpirationCheckpoint> ExpirationCheckpoints
        => _database.GetCollection<ExpirationCheckpoint>("expiration_checkpoints");

    // Stok snapshot'larının tutulduğu collection.
    public IMongoCollection<InventorySnapshot> Snapshots
    => _database.GetCollection<InventorySnapshot>("inventory_snapshots");

    //MongoDB Client nesnesini döner.
    public IMongoClient Client => _client;
}
