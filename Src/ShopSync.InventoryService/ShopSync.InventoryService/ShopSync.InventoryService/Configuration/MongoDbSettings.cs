namespace ShopSync.InventoryService.Configuration;

public sealed class MongoDbSettings
{
    // MongoDB bağlantı ayarlarını yapılandırmak için kullanılır.
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
