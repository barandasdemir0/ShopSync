namespace ShopSync.OrderService.Configuration;

// Bu ayarlar, gRPC sunucusunun adresini içerir ve uygulamanın gRPC hizmetine bağlanabilmesi için kullanılır.
public sealed class InventoryGrpcSettings
{
    public string Address { get; set; } = string.Empty;
}
