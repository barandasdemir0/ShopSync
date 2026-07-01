namespace ShopSync.InventoryService.Configuration;

public sealed class RedisSettings
{
    // Redis bağlantı ayarlarını yapılandırmak için kullanılır.
    public string ConnectionString { get; set; } = string.Empty;

}
