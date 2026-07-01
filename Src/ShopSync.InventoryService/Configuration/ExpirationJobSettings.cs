namespace ShopSync.InventoryService.Configuration;

public sealed class ExpirationJobSettings
{
    //job 2 dakika aralıklarla çalışacak
    public int IntervalMinutes { get; set; } = 2;

    //bir rezervasuyonun süresi 10 dakika olacak
    public int ExpirationMinutes { get; set; } = 10;


}
