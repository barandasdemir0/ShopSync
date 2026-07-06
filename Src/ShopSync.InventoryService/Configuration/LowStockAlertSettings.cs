namespace ShopSync.InventoryService.Configuration;

public sealed class LowStockAlertSettings
{
    //uyarı kontrolü
    public int IntervalMinutes { get; set; } = 5;
}
